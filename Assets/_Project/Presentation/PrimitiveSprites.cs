using UnityEngine;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Спрайты из кода — без ассетов в проекте. Один белый 1×1 спрайт переиспользуется для всех
    /// прямоугольников (ноты, клавиши, линии); конкретный цвет задаётся через
    /// <c>SpriteRenderer.color</c>, масштаб — через <c>transform.localScale</c>.
    /// </summary>
    public static class PrimitiveSprites
    {
        private static Sprite _white;

        public static Sprite White()
        {
            if (_white != null) return _white; // Unity-проверка времени жизни (учитывает уничтожение)

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit: 1f);
            _white.name = "RacconotesWhite1x1";
            return _white;
        }
    }
}
