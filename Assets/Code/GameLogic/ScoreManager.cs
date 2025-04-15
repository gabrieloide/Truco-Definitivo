using Mirror;
using UnityEngine;

namespace Code.GameLogic
{
    public class ScoreManager : MonoBehaviour
    {
        public void IncreaseScore(string teamPlayer)
        {
            if ("Team 1" == teamPlayer)
            {
                GameManager.Instance.teams[0].roundsWon++;
            }
            else if (teamPlayer == "Team 2")
            {
                GameManager.Instance.teams[1].roundsWon++;
            }

            foreach (var team in GameManager.Instance.teams)
            {
                if (team.roundsWon == 2)
                {
                    team.teamScore += AmountToIncrease();
                    ResetTeamRoundWon();
                }
            }
        }

        private void ResetTeamRoundWon()
        {
            foreach (var team in GameManager.Instance.teams)
            {
                team.roundsWon = 0;
            }
        }

        private int AmountToIncrease()
        {
            var amount = 1;

            return amount;
        }
    }
}