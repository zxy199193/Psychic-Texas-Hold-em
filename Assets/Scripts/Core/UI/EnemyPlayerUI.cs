using UnityEngine;
using UnityEngine.UI;

public class EnemyPlayerUI : MonoBehaviour
{
    [Header("Seat & Hand Info")]
    public GameObject seatNode;
    public Transform handArea;
    public Transform dealerPos;

    [Header("Player Details")]
    public Text nameText;
    public Text chipsText;
    public Text currentBetText;
    public Text energyText;
    public RawImage avatarImage;

    [Header("Status Nodes")]
    public GameObject rebuyNode;
    public Text rebuyText;
    public GameObject foldNode;
    public GameObject disconnectNode;
    public GameObject turnHighlightNode;
    public GameObject hostingNode;
    public GameObject countdownNode;
    public Text countdownText;

    [Header("Game Results")]
    public GameObject handTypeNode;
    public Text handTypeText;
    public GameObject winnerNode;

    [Header("Trinkets UI")]
    public Transform trinketContainer;
    public GameObject[] trinketSlots;
}
