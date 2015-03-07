using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CitizenFX.Core;

using CitizenWorld.DAL;

namespace CitizenWorld
{
    public class TeamSelection : BaseScript
    {
        private bool m_allowSelectTeam = false;

        public static int LocalTeam { get; private set; }

        public TeamSelection()
        {
            EventHandlers["onClientGameTypeStart"] += new Action<string>(async resource =>
            {
                var selectedTeams = await Data<PlayerTeamAssignment>.GetAsync(new { PlayerGuid = new MetaField("playerGuid") });
                var selectedTeam = selectedTeams.FirstOrDefault();

                if (selectedTeam == null)
                {
                    Debug.WriteLine("Launching team UI.");

                    LaunchTeamUI();
                }
                else
                {
                    Debug.WriteLine("Requesting spawning for team {0}.", selectedTeam.TeamId);

                    TriggerServerEvent("ocw:checkSpawnTeam", selectedTeam.TeamId);
                }
            });

            EventHandlers["ocw:spawnForTeam"] += new Action<int>(teamId =>
            {
                Debug.WriteLine("Spawning for team {0}.", teamId);

                LocalTeam = teamId;

                Function.Call(Natives.SET_PLAYER_TEAM, LocalPlayer.ID, teamId);

                TriggerEvent("ocw:onPlayerSignedIn");
            });

            EventHandlers["ocw:chatCommand"] += new Action<string>(async message =>
            {
                var parts = message.Split(' ');

                if (parts.Length == 3)
                {
                    if (parts[1] == "team")
                    {
                        if (m_allowSelectTeam)
                        {
                            var teamId = int.Parse(parts[2]);

                            if (teamId == 1 || teamId == 2)
                            {
                                await SelectTeam(teamId);
                            }
                        }
                    }
                }
            });
        }

        private void LaunchTeamUI()
        {
            // TODO: actual UI?
            m_allowSelectTeam = true;
        }

        private async Task SelectTeam(int teamId)
        {
            var teamAssignment = new PlayerTeamAssignment();
            teamAssignment.TeamId = teamId;
            
            var result = await teamAssignment.SaveAsync();

            if (result)
            {
                m_allowSelectTeam = false;

                TriggerServerEvent("ocw:checkSpawnTeam", teamId);
            }
        }
    }

    class PlayerTeamAssignment : DataObject<PlayerTeamAssignment>
    {
        public string PlayerGuid { get; set; }

        public int TeamId { get; set; }
    }
}
