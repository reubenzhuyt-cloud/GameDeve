using UnityEngine;

[ExecuteAlways]
public class SimpleShapeRenderer : MonoBehaviour
{
    public enum ShapeType
    {
        Square,
        Circle,
        Triangle,
        Diamond
    }
    
    public ShapeType shapeType = ShapeType.Square;
    public Color color = Color.white;
    public Vector2 size = new Vector2(1f, 1f);
    public bool autoGenerate = true;
    
    private SpriteRenderer spriteRenderer;
    private Texture2D texture;
    private Sprite sprite;
    
    private void Awake()
    {
        if (autoGenerate)
        {
            GenerateShape();
        }
    }
    
    private void Start()
    {
        if (autoGenerate)
        {
            GenerateShape();
        }
    }
    
    private void OnValidate()
    {
        if (autoGenerate && Application.isEditor)
        {
            GenerateShape();
        }
    }
    
    public void GenerateShape()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        int resolution = 64;
        texture = new Texture2D(resolution, resolution);
        texture.filterMode = FilterMode.Point;
        
        Color[] pixels = new Color[resolution * resolution];
        
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = (float)x / resolution;
                float normalizedY = (float)y / resolution;
                
                bool insideShape = IsInsideShape(normalizedX, normalizedY, resolution);
                pixels[y * resolution + x] = insideShape ? color : Color.clear;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        sprite = Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
        spriteRenderer.sprite = sprite;
        
        transform.localScale = new Vector3(size.x, size.y, 1f);
    }
    
    private bool IsInsideShape(float x, float y, int resolution)
    {
        float centerX = 0.5f;
        float centerY = 0.5f;
        float dx = x - centerX;
        float dy = y - centerY;
        
        switch (shapeType)
        {
            case ShapeType.Square:
                return Mathf.Abs(dx) <= 0.45f && Mathf.Abs(dy) <= 0.45f;
            
            case ShapeType.Circle:
                return dx * dx + dy * dy <= 0.45f * 0.45f;
            
            case ShapeType.Triangle:
                float normalizedDY = 0.5f - y;
                float halfWidth = 0.5f * (1f - normalizedDY * 2f);
                return normalizedDY >= -0.45f && Mathf.Abs(dx) <= halfWidth;
            
            case ShapeType.Diamond:
                return Mathf.Abs(dx) + Mathf.Abs(dy) <= 0.45f;
            
            default:
                return false;
        }
    }
    
    private void OnDestroy()
    {
        if (texture != null)
        {
            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
        }
    }
}
