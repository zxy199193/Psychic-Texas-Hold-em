using UnityEngine;
using UnityEngine.UI;

public class LeaderboardItemUI : MonoBehaviour
{
    [Header("UI 控件绑定")]
    public Text txtRank;
    public Text txtPlayerName;
    public Text txtChips;

    public void Setup(int rank, string playerName, int chips)
    {
        if (txtRank != null)
        {
            txtRank.text = rank.ToString();
        }

        if (txtPlayerName != null)
        {
            txtPlayerName.text = string.IsNullOrEmpty(playerName) ? "Unknown Player" : playerName;
        }

        if (txtChips != null)
        {
            txtChips.text = chips.ToString();
        }
    }
}
