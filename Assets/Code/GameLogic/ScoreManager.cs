using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.GameLogic
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager instance { get; private set; }
        public int amountToIncrease;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

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
                    team.teamScore += 1 + amountToIncrease;
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
    }
}