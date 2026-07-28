using UnityEngine;
using UnityEngine.UI;

public class SkillInfoItemUI : MonoBehaviour
{
    public Text txtName;
    public Image imgIcon;
    public Text txtCost;
    public Text txtTime;
    public Text txtDescription;
    public GameObject lockMask;

    public void Setup(string skillName, Sprite icon, int cost, float time, string description, bool isUnlocked)
    {
        if (txtName != null) txtName.text = skillName;
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
        if (txtCost != null) txtCost.text = cost < 0 ? "X" : cost.ToString();
        if (txtTime != null) txtTime.text = time > 0 ? time.ToString("0.##") : "0";
        if (txtDescription != null) txtDescription.text = description;
        if (lockMask != null) lockMask.SetActive(!isUnlocked);
    }
}
