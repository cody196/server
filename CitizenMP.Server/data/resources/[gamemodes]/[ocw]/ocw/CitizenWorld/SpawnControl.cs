using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CitizenFX.Core;
using CitizenWorld.DAL;

namespace CitizenWorld
{
    public class SpawnControl : BaseScript
    {
        private SpawnPoint m_localSpawnPoint;

        public SpawnControl()
        {
            //EventHandlers["ocw:onPlayerSignedIn"] += new Action(async () =>
            EventHandlers["onClientGameTypeStart"] += new Action<string>(async (a) =>
            {
                /*var spawnPoints = await Data<SpawnPoint>.GetAsync(new { PlayerGuid = new MetaField("playerGuid") });
                var spawnPoint = spawnPoints.FirstOrDefault();

                if (spawnPoint == null)
                {
                    Debug.WriteLine("new spawn");

                    spawnPoint = new SpawnPoint();
                    spawnPoint.SpawnPositionX = 21.001f;
                    spawnPoint.SpawnPositionY = -40.001f;
                    spawnPoint.SpawnPositionZ = 15.001f;

                    var res = await spawnPoint.SaveAsync();

                    Debug.WriteLine("new spawn res {0}", res);
                }*/

                var spawnPoint = new SpawnPoint();
                spawnPoint.SpawnPositionX = 21.001f;
                spawnPoint.SpawnPositionY = -40.001f;
                spawnPoint.SpawnPositionZ = 15.001f;

                m_localSpawnPoint = spawnPoint;

                Exports["spawnmanager"].spawnPlayer(new
                {
                    x = spawnPoint.SpawnPositionX,
                    y = spawnPoint.SpawnPositionY,
                    z = spawnPoint.SpawnPositionZ,
                    heading = 0.0f,
                    model = 1343144208
                });

                /*Exports["spawnmanager"].addSpawnPoint(new
                {
                    x = 21.001f, y = -40.001f, z = 15.001f, model = "ig_brucie", heading = 0.1f
                });

                Exports["spawnmanager"].setAutoSpawn(true);

                Exports["spawnmanager"].forceRespawn();*/
            });

            // respawning
            EventHandlers["onPlayerWasted"] += new Action<int>(async playerId =>
            {
                if (playerId == LocalPlayer.ServerId)
                {
                    await Delay(1500);

                    Exports["spawnmanager"].spawnPlayer(new
                    {
                        x = 21.001f, y = -40.001f, z = 15.001f, heading = 180.01f
                    });
                }
            });

            // saving last on-foot location
            //Tick += SpawnControl_Tick;
        }

        async Task SpawnControl_Tick()
        {
            await Delay(30000);

            var ped = LocalPlayer.Ped;

            // if we're on a safe on-foot position
            if (ped.CurrentVehicle == null && !ped.IsInjured && !ped.IsInAir)
            {
                var position = ped.Position;
                m_localSpawnPoint.SpawnPositionX = position.X;
                m_localSpawnPoint.SpawnPositionY = position.Y;
                m_localSpawnPoint.SpawnPositionZ = position.Z;

                var result = await m_localSpawnPoint.SaveAsync();

                if (!result)
                {
                    Debug.WriteLine("Something went wrong while saving spawn point?");
                }
            }
        }
    }

    class SpawnPoint : DataObject<SpawnPoint>
    {
        public string PlayerGuid { get; set; }

        public float SpawnPositionX { get; set; }
        public float SpawnPositionY { get; set; }
        public float SpawnPositionZ { get; set; }
    }
}
