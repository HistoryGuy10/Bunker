using UnityEngine;

public class Random : MonoBehaviour
{
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] Sprite[] sprites;
    private void Start()
    {
        var randomIndex = UnityEngine.Random.Range(0, sprites.Length);

        spriteRenderer.sprite = sprites[randomIndex];

        var random = UnityEngine.Random.Range(0f, 1f);

        spriteRenderer.flipX = random > 0.5f;
    }
}
