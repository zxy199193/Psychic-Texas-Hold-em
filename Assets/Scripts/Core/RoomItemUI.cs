using UnityEngine;
using UnityEngine.UI;

public class RoomItemUI : MonoBehaviour
{
    public Text txtHostName;
    public RawImage imgHostAvatar;
    public Text txtPlayerCount;
    public Button btnJoin;

    [HideInInspector] public ulong steamLobbyId;
}
