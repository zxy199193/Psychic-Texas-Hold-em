using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PokerCardAnimator : MonoBehaviour
{
    public List<GameObject> dealRound1 = new List<GameObject>();
    public List<GameObject> dealRound2 = new List<GameObject>();
    public List<GameObject> dealCommunity = new List<GameObject>();
    private bool isDealScheduled = false;

    private PokerUIManager UIMgr => PokerUIManager.Instance;

    public void PrepareCardForFlight(GameObject cardObj, List<GameObject> targetList)
    {
        cardObj.transform.localScale = Vector3.zero;
        targetList.Add(cardObj);
    }

    public void ScheduleMasterDeal()
    {
        if (!isDealScheduled)
        {
            isDealScheduled = true;
            StartCoroutine(MasterDealRoutine());
        }
    }

    private System.Collections.IEnumerator MasterDealRoutine()
    {
        yield return new WaitForEndOfFrame();
        isDealScheduled = false;

        ForceRebuildAllAreas();

        float dealInterval = 0.15f;

        foreach (var card in dealRound1)
        {
            if (card != null)
            {
                StartCoroutine(FlySingleCard(card));
                yield return new WaitForSeconds(dealInterval);
            }
        }

        foreach (var card in dealRound2)
        {
            if (card != null)
            {
                StartCoroutine(FlySingleCard(card));
                yield return new WaitForSeconds(dealInterval);
            }
        }

        foreach (var card in dealCommunity)
        {
            if (card != null)
            {
                StartCoroutine(FlySingleCard(card));
                yield return new WaitForSeconds(0.1f);
            }
        }

        dealRound1.Clear();
        dealRound2.Clear();
        dealCommunity.Clear();
    }

    private void ForceRebuildAllAreas()
    {
        if (UIMgr.myHandArea != null) LayoutRebuilder.ForceRebuildLayoutImmediate(UIMgr.myHandArea.GetComponent<RectTransform>());
        if (UIMgr.communityArea != null) LayoutRebuilder.ForceRebuildLayoutImmediate(UIMgr.communityArea.GetComponent<RectTransform>());
        if (UIMgr.enemyHandAreas != null)
        {
            foreach (var area in UIMgr.enemyHandAreas)
                if (area != null) LayoutRebuilder.ForceRebuildLayoutImmediate(area.GetComponent<RectTransform>());
        }
    }

    private System.Collections.IEnumerator FlySingleCard(GameObject cardObj)
    {
        if (UIMgr.deckOriginPos == null)
        {
            cardObj.transform.localScale = Vector3.one;
            yield break;
        }

        Vector3 targetWorldPos = cardObj.transform.position;
        cardObj.transform.position = UIMgr.deckOriginPos.position;

        if (AudioManager.Instance != null) AudioManager.Instance.PlayDealCard();

        float t = 0;
        Vector3 startWorldPos = cardObj.transform.position;

        while (t < 1f)
        {
            t += Time.deltaTime / UIMgr.cardFlySpeed;
            float ease = Mathf.SmoothStep(0f, 1f, t);

            cardObj.transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, ease);
            cardObj.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, ease);
            yield return null;
        }

        cardObj.transform.position = targetWorldPos;
        cardObj.transform.localScale = Vector3.one;
    }
}
