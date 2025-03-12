using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CreateDam : MonoBehaviour
{
    [Range(1, 100)] public int floodFillThreashold;
    public Color boundaryColor = Color.blue;
    public Color fillColor = Color.red;

    private SpriteRenderer spriteRenderer;
    private Texture2D textureDam;
    private float maxWaterLevel = 0f;
    private Texture2D textureOriginal;
    public bool CreatedDam { get; set; }

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        textureOriginal = spriteRenderer.sprite.texture;

        textureDam = new Texture2D(textureOriginal.width, textureOriginal.height, textureOriginal.format, false);
        Graphics.CopyTexture(textureOriginal, textureDam);

        spriteRenderer.sprite = Sprite.Create(textureDam, spriteRenderer.sprite.rect, new Vector2(0.5f, 0.5f), 200);
    }

    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int pixelPos = WorldToPixel(mousePos);

            if (pixelPos.x >= 0 && pixelPos.x < textureDam.width && pixelPos.y >= 0 && pixelPos.y < textureDam.height)
            {
                textureDam.SetPixel(pixelPos.x, pixelPos.y, boundaryColor);
                textureDam.Apply();
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int pixelPos = WorldToPixel(mousePos);

            if (pixelPos.x >= 0 && pixelPos.x < textureDam.width && pixelPos.y >= 0 && pixelPos.y < textureDam.height)
            {
                FloodFill(textureDam, pixelPos.x, pixelPos.y, textureDam.GetPixel(pixelPos.x, pixelPos.y));
                textureDam.Apply(); // Apply changes

                // Assign updated texture
                spriteRenderer.sprite = Sprite.Create(textureDam, spriteRenderer.sprite.rect, new Vector2(0.5f, 0.5f), 200);

                CreatedDam = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            textureDam.SetPixels(textureOriginal.GetPixels());
            textureDam.Apply();
            spriteRenderer.sprite = Sprite.Create(textureDam, spriteRenderer.sprite.rect, new Vector2(0.5f, 0.5f), 200);

            CreatedDam = false;
        }
    }

    Vector2Int WorldToPixel(Vector2 worldPos)
    {
        Bounds bounds = spriteRenderer.bounds; // Get the visible bounds of the sprite
        Vector2 min = bounds.min; // Bottom-left corner of the sprite in world space
        Vector2 max = bounds.max; // Top-right corner of the sprite in world space

        float xNormalized = Mathf.Clamp01((worldPos.x - min.x) / (max.x - min.x));
        float yNormalized = Mathf.Clamp01((worldPos.y - min.y) / (max.y - min.y));

        return new Vector2Int(Mathf.RoundToInt(xNormalized * textureDam.width), Mathf.RoundToInt(yNormalized * textureDam.height));
    }

    private bool IsColorInRange(Color color, Color min, Color max)
    {
        return color.r >= min.r && color.r <= max.r &&
               color.g >= min.g && color.g <= max.g &&
               color.b >= min.b && color.b <= max.b &&
               color.a >= min.a && color.a <= max.a;
    }

    private void FloodFill(Texture2D texture, int x, int y, Color oldColor)
    {
        if (oldColor == fillColor || oldColor == boundaryColor) return;

        Color range = floodFillThreashold / 100f * Color.white;
        var maxColor = oldColor + range;

        Stack<Vector2Int> pixels = new();
        pixels.Push(new Vector2Int(x, y));

        while (pixels.Count > 0)
        {
            Vector2Int p = pixels.Pop();
            if (p.x < 0 || p.x >= texture.width || p.y < 0 || p.y >= texture.height) continue;

            Color pixelColor = texture.GetPixel(p.x, p.y);

            if (!IsColorInRange(pixelColor, Color.black, maxColor) || pixelColor == boundaryColor) continue;

            texture.SetPixel(p.x, p.y, fillColor);
            maxWaterLevel = Mathf.Max(maxWaterLevel, textureOriginal.GetPixel(p.x, p.y).grayscale);

            pixels.Push(new Vector2Int(p.x + 1, p.y));
            pixels.Push(new Vector2Int(p.x - 1, p.y));
            pixels.Push(new Vector2Int(p.x, p.y + 1));
            pixels.Push(new Vector2Int(p.x, p.y - 1));
        }
    }

    public bool[,] GetSelectedPixels()
    {
        var selected = new bool[textureDam.width, textureDam.height];

        for (int x = 0; x < textureDam.width; x++)
        {
            for (int y = 0; y < textureDam.height; y++)
            {
                if (textureDam.GetPixel(x, y) == fillColor)
                {
                    selected[x, y] = true;
                }
            }
        }

        return selected;
    }

    public float GetMaxElevationSelected()
    {
        var maxElevation = 0.0f;

        for (int x = 0; x < textureDam.width; x++)
        {
            for (int y = 0; y < textureDam.height; y++)
            {
                if (textureDam.GetPixel(x, y) == fillColor)
                {
                    float elevation = textureOriginal.GetPixel(x, y).grayscale;
                    maxElevation = Mathf.Max(maxElevation, elevation);
                }
            }
        }

        return maxElevation;
    }

    public float GetPixelSize()
    {
        return transform.localScale.x / textureOriginal.width;
    }
}
