using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using VRageMath;
using VRage.Common.Utils;
using SEModAPIExtensions.API.Plugin;
using SEModAPIExtensions.API.Plugin.Events;
using SEModAPIExtensions.API;
using SEModAPI.API;
using SEModAPIInternal.API.Common;
using SEModAPIInternal.API.Entity;
using SEModAPIInternal.API.Entity.Sector.SectorObject;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid.CubeBlock;
using SEModAPIInternal.Support;
using System.ComponentModel;
using System.Timers;
using System.Reflection;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.VRageData;
using SEModAPIInternal.API.Server;

namespace SEKingOfTheHillPlugin
{
    public class Core : PluginBase, IChatEventHandler
    {
        #region "Properties"

        private String hillPath;

        private Thread mainloop;
        private bool m_running;


        private Dictionary<Faction, Int32> roundPoints;
        private Dictionary<Faction, Int32> gamePoints;

       
        private Int32 roundIntervalSeconds = 600;

        [Category("SE King Of The Hill")]
        [Description("Round Interval In Seconds")]
        [Browsable(true)]
        [ReadOnly(false)]
        public Int32 RoundInterval
        {
            get { return roundIntervalSeconds; }
            set { roundIntervalSeconds = value; }
        }

        [Category("SE King Of The Hill")]
        [Description("X Position of the hill")]
        [Browsable(true)]
        [ReadOnly(false)]
        public float HillPositionX
        {
            get { return hillPosition.X; }
            set { hillPosition.X = value; }
        }
        [Category("SE King Of The Hill")]
        [Description("Y Position of the hill")]
        [Browsable(true)]
        [ReadOnly(false)]
        public float HillPositionY
        {
            get { return hillPosition.Y; }
            set { hillPosition.Y = value; }
        }
        [Category("SE King Of The Hill")]
        [Description("Z Position of the hill")]
        [Browsable(true)]
        [ReadOnly(false)]
        public float HillPositionZ
        {
            get { return hillPosition.Z; }
            set { hillPosition.Z = value; }
        }

        private Vector3 hillPosition = new Vector3(0,0,0);
        private long hillEntityID = -1;
        private int strikes = 0;
        private bool errorLastLoop = false;

        private Int32 roundSecondsRemaining;
       
        private Faction previousOwner = null;
       
        #endregion



        public override void Init()
        {
            roundPoints = new Dictionary<Faction, Int32>();
            gamePoints = new Dictionary<Faction, Int32>();
            roundSecondsRemaining = roundIntervalSeconds;
            hillPath = Path.Combine(MyFileSystem.ModsPath, "KingOfTheHill");
            hillPath = Path.Combine(hillPath,"hill.sbc");
            Console.WriteLine("King of the Hill plugin initialised!");

            m_running = true;
            mainloop = new Thread(main);
            mainloop.Priority = ThreadPriority.BelowNormal;
            mainloop.Start();
        }

        public override void Update()
        {
            
        }

        public override void Shutdown()
        {
            m_running = false;

            //shut down main loop
            mainloop.Join(1000);
            mainloop.Abort();

            return;
        }

        private void main()
        {
            while (m_running)
            {
                try
                {
                    Thread.Sleep(1000);
                    //  Console.WriteLine("Doing tick! " + roundSecondsRemaining + " seconds remaining in round.");
                    //check on hill, make sure it's there and replace it if it isn't
                    CubeGridEntity hill = getHill();

                    MyObjectBuilder_Beacon beacon = null;
                    if (hill != null)
                    {
                        //   Console.WriteLine("Got the hill, looking for beacon");

                        foreach (MyObjectBuilder_CubeBlock cubeBlock in hill.BaseCubeBlocks)
                        {
                            if (cubeBlock.TypeId == typeof(MyObjectBuilder_Beacon))
                            {
                                if (((MyObjectBuilder_Beacon)cubeBlock).CustomName.Equals("The Hill"))
                                    beacon = (MyObjectBuilder_Beacon)cubeBlock;
                            }
                        }
                        //need to check for power & integrity here, set beacon to null if they're not acceptable

                        if (beacon == null)
                        {
                            strikes++;
                            Console.WriteLine("Could not find beacon! Strike " + strikes);
                            if (strikes >= 3) //there seems to be an issue where listing blocks in a cube grid is not 100% accurate. This is just a failsafe.
                            {
                                ChatManager.Instance.SendPublicChatMessage("The hill has been freed!");
                                Console.WriteLine("Hill beacon has been destroyed!");
                                createNewHill();
                                strikes = 0;
                            }
                            else
                            {
                                awardRoundPoint(previousOwner);
                            }
                        }
                        else
                        {
                            strikes = 0;
                            beacon.CustomName = "The Hill";
                            beacon.BroadcastRadius = -1; //force infinity
                            beacon.Enabled = true;



                            //hill exists, and is working - check for ownership and award a point
                            //Console.WriteLine("Beacon owner is " + beacon.Owner);
                            Faction pointWinner = null;
                            foreach (Faction f in FactionsManager.Instance.Factions)
                            {
                                foreach (FactionMember m in f.Members)
                                    if (m.PlayerId == beacon.Owner)
                                        pointWinner = f;
                            }
                            if (pointWinner != previousOwner && pointWinner != null)
                                ChatManager.Instance.SendPublicChatMessage("The beacon has been captured by " + pointWinner.Name + "!");
                            previousOwner = pointWinner;

                            awardRoundPoint(pointWinner);
                        }

                    }
                    else
                    {
                        ChatManager.Instance.SendPublicChatMessage("The hill has been freed!");
                        Console.WriteLine("No hill found! Creating new one.");
                        createNewHill();
                    }

                    roundSecondsRemaining--;
                    if (roundSecondsRemaining % 30 == 0)
                        ChatManager.Instance.SendPublicChatMessage("There are " + roundSecondsRemaining + " seconds remaining in this round!");

                    if (roundSecondsRemaining <= 10)
                        ChatManager.Instance.SendPublicChatMessage("This round will finish in " + roundSecondsRemaining + " seconds!");


                    if (roundSecondsRemaining <= 0)
                    {
                        roundSecondsRemaining = roundIntervalSeconds;
                        Faction winningFaction = null;
                        Int32 winningScore = 0;
                        foreach (Faction f in roundPoints.Keys)
                        {
                            Int32 factionScore = 0;
                            roundPoints.TryGetValue(f, out factionScore);
                            if (factionScore > winningScore)
                            {
                                winningFaction = f;
                                winningScore = factionScore;
                            }
                        }
                        if (winningFaction != null)
                        {
                            awardGamePoint(winningFaction);
                            Console.WriteLine(winningFaction.Name + " won the round with " + winningScore + " points!");
                            ChatManager.Instance.SendPublicChatMessage(winningFaction.Name + " won the round with " + winningScore + " points!");
                        }
                        else
                        {
                            ChatManager.Instance.SendPublicChatMessage("No faction won this round!");
                            Console.WriteLine("No faction won this round!");
                        }
                        //createNewHill(); //only create new hill when it's been destroyed
                        roundPoints.Clear(); //start a new round
                        ChatManager.Instance.SendPublicChatMessage("A new round has been started. It will last for " + roundIntervalSeconds + " seconds!");
                    }
                    errorLastLoop = false;

                }
                catch(Exception e)
                {
                    Console.WriteLine("Exception occurred in main thread of King Of The Hill Plugin. Details: " + e);
                    if (errorLastLoop)
                    {
                        Console.WriteLine("Unable to continue, disabling King Of The Hill Plugin!");
                        try
                        {
                            ChatManager.Instance.SendPublicChatMessage("An error has occurred in the King of the Hill plugin, an it has been disabled. Please contact the server admin.");
                        }
                        catch (Exception chatexception)
                        {
                            //discard exceptions here, we were just trying to warn people, it doesn't matter if it fails
                        }
                        m_running = false; //terminate the main thread
                    }
                    else
                    {
                        errorLastLoop = true;
                        Console.WriteLine("Attempting to continue...");
                    }
                }
            }
        }



        //function returns either the CubeGridEntity describing the Hill, or null if no Hill is found
        private CubeGridEntity getHill()
        {
          //  Console.WriteLine("Getting the hill...");
            if (hillEntityID == -1)
                return null;

            List<CubeGridEntity> entitites = SectorObjectManager.Instance.GetTypedInternalData<CubeGridEntity>();
            foreach(CubeGridEntity c in entitites)
            {
             //   Console.WriteLine("Entity at " + c.Position);
                if (Vector3.Distance(c.Position,hillPosition) < 1 && (c.EntityId == hillEntityID || hillEntityID == 0))
                {
                 //   Console.WriteLine("Found the hill!");
                    hillEntityID = c.EntityId;
                    return c;
                }
            }
            Console.WriteLine("No hill found!");
          
            return null;
        }

        //deletes the currentHill if not null, then creates a new one
        private void createNewHill()
        {
            try
            {
                List<CubeGridEntity> entitites = SectorObjectManager.Instance.GetTypedInternalData<CubeGridEntity>();
                
                foreach (CubeGridEntity c in entitites)
                {
                    if (Vector3.Distance(c.Position,hillPosition) < 20) //clear space for the hill
                    {
                        Console.WriteLine("Deleting entity at " + c.Position + " with name " + c.Name+" and EntityID "+c.EntityId);
                        foreach (CubeBlockEntity cb in c.CubeBlocks)
                            cb.BuildPercent = -1;

                        c.Dispose();
                    }
                }
                
                Console.WriteLine("Creating new hill from " + hillPath);
                
                FileInfo importFile = new FileInfo(hillPath);
                if (!File.Exists(importFile.FullName))
                {
                    Console.WriteLine("HILL FILE DOES NOT EXIST!");                    
                }
                else
                {
                    CubeGridEntity TheHill = new CubeGridEntity(importFile);
                    TheHill.Position = hillPosition;                    
                    SectorObjectManager.Instance.AddEntity(TheHill);
                    hillEntityID = TheHill.EntityId;
                    Thread.Sleep(500);
                    Console.WriteLine("Done! HillID:"+hillEntityID);
                    
                }
            }
            catch (GameInstallationInfoException e)
            {
                Console.WriteLine("Could not create hill: "+e);        
                Console.WriteLine("Additional Info:" + e.StateRepresentation[e.ExceptionStateId]);
            }
        }


        //awards a round point to the faction
        private void awardRoundPoint(Faction faction)
        {
            if (faction == null)
                return;

            Int32 points = 0;
            if (roundPoints.ContainsKey(faction))
            {
                roundPoints.TryGetValue(faction, out points);
                points++;
                roundPoints[faction] = points;
            }
            else
            {
                points = 1;
                roundPoints.Add(faction, points);
            }

         

            
        }

        //awards a game point to the faction
        private void awardGamePoint(Faction faction)
        {
            if (faction == null)
                return;

            Int32 points = 0;
            if (gamePoints.ContainsKey(faction))
            {
                gamePoints.TryGetValue(faction, out points);
                points++;
                gamePoints[faction] = points;
            }
            else
            {
                points = 1;
                gamePoints.Add(faction, points);
            }
        }

        public void OnChatSent(ChatManager.ChatEvent ce)
        {
        }

        public void OnChatReceived(ChatManager.ChatEvent ce)
        {

            if (ce.sourceUserId == 0)
                return;

            if (ce.message == "/leaderboard")
            {
                List<Tuple<Faction, Int32>> scores = new List<Tuple<Faction, Int32>>();
                foreach (Faction f in gamePoints.Keys)
                {
                    Int32 points = 0;
                    int i = 0;
                    bool placed = false;
                    gamePoints.TryGetValue(f, out points);
                    while (i < scores.Count && !placed)
                    {
                        if (scores[i].Item2 > points)
                            placed = true;
                        else
                            i++;
                    }
                    scores.Insert(i, new Tuple<Faction, int>(f, points));
                }

                String leadermessage = "Top "+Math.Min(scores.Count, 5)+":\n";
                for (int i = 0; i < Math.Min(scores.Count, 5); i++)
                {
                    leadermessage += scores[i].Item1.Name + ": " + scores[i].Item2 + " wins\n";
                }
                ChatManager.Instance.SendPrivateChatMessage(ce.sourceUserId, leadermessage);
            }
        }

    }
}
