using UnityEngine;
using UnityEngine.UI;

public class TrinketInfoItemUI : MonoBehaviour
{
    public Text txtName;
    public Image imgIcon;
    public Text txtDescription;
    public GameObject lockMask;

    public void Setup(string trinketName, Sprite icon, string description, bool isUnlocked)
    {
        if (txtName != null) txtName.text = trinketName;
        if (imgIcon != null)
        {
            if (icon != null)
            {
                imgIcon.gameObject.SetActive(true);
                imgIcon.sprite = icon;
            }
            else
            {
                imgIcon.gameObject.SetActive(false);
            }
        }
        if (txtDescription != null) txtDescription.text = description;
        if (lockMask != null) lockMask.SetActive(!isUnlocked);
    }
}
