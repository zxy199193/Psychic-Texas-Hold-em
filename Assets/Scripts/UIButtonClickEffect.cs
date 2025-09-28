using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class UIButtonClickEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("���Ų���")]
    public float scaleFactor = 0.9f;       // ����ʱ��С����
    public float scaleSpeed = 10f;         // ���Ŷ����ٶ�

    [Header("��Ч (��ѡ)")]
    public AudioClip clickSound;
    public float volume = 1f;

    private Vector3 originalScale;
    private Vector3 targetScale;
    private AudioSource audioSource;

    private void Awake()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;

        if (clickSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private void Update()
    {
        // ƽ������
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = originalScale * scaleFactor;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(clickSound, volume);
        }
    }
}
