using Unity.VisualScripting;
using UnityEngine;

public class Shield : MonoBehaviour
{
    [SerializeField] private int maxHits = 3;
    [SerializeField] private float fadeSpeed = 2f;
    
    private int currentHits = 0;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private float duration;
    private float spawnTime;

    void Awake()
    {
        spriteRenderer = transform.GetChild(0).GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    public void Initialize(float shieldDuration)
    {
        duration = shieldDuration;
        spawnTime = Time.time;
        currentHits = 0;
    }

    void Update()
    {
        if (spriteRenderer != null && currentHits > 0)
        {
            float alpha = Mathf.Lerp(1f, 0.3f, (float)currentHits / maxHits);
            Color targetColor = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            spriteRenderer.color = Color.Lerp(spriteRenderer.color, targetColor, Time.deltaTime * fadeSpeed);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            TakeDamage();
        }
    }

    private void TakeDamage()
    {
        ++currentHits;
        
        if (currentHits >= maxHits)
        {
            DestroyShield();
        }
        else
        {
            if (spriteRenderer != null)
            {
                StartCoroutine(FlashEffect());
            }
        }
    }

    private System.Collections.IEnumerator FlashEffect()
    {
        if (spriteRenderer == null) yield break;

        Color flashColor = Color.white;
        spriteRenderer.color = flashColor;
        
        yield return new WaitForSeconds(0.1f);
        
        float alpha = Mathf.Lerp(1f, 0.3f, (float)currentHits / maxHits);
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
    }

    private void DestroyShield()
    {
        Destroy(gameObject);
    }
}