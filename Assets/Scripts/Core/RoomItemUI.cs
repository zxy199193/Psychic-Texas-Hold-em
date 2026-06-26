using UnityEngine;
using UnityEngine.UI;

public class RoomItemUI : MonoBehaviour
{
    public Text txtHostName;
    public RawImage imgHostAvatar;
    public Text txtPlayerCount;
    public Button btnJoin;

    // 新增：模式与玩家列表支持
    public Text txtMode;
    public Transform playerListContainer;
    public GameObject playerIconPrefab;

    [HideInInspector] public ulong steamLobbyId;

    private void Awake()
    {
        // 自动寻路，防止 Inspector 未拖拽导致 NullReference
        if (txtHostName == null) txtHostName = transform.Find("Text Name")?.GetComponent<Text>();
        if (imgHostAvatar == null) imgHostAvatar = transform.Find("RawImage Steam Avatar")?.GetComponent<RawImage>();
        if (txtPlayerCount == null) txtPlayerCount = transform.Find("HL/Text Num")?.GetComponent<Text>();
        if (btnJoin == null) btnJoin = transform.Find("Button Join")?.GetComponent<Button>();
        
        if (txtMode == null) txtMode = transform.Find("Image Mode/Text Mode")?.GetComponent<Text>();
        if (playerListContainer == null) playerListContainer = transform.Find("Player List HL");
    }
}
