using UnityEngine;
using Racconotes.Application;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Точка входа слоя Presentation. Срабатывает после загрузки сцены (AfterSceneLoad), то есть
    /// уже после <c>RuntimeBootstrap</c> (BeforeSceneLoad), который открыл БД и опубликовал
    /// <see cref="GameServices.Context"/>. Создаёт игровой объект с <see cref="AppController"/>
    /// (координатор экранов меню↔игра) из кода — сцену править не нужно (никакого ручного YAML).
    /// </summary>
    public static class PresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (!GameServices.IsReady)
            {
                Debug.LogError("[Racconotes] Presentation: GameServices не готов (RuntimeBootstrap не отработал?). Приложение не запущено.");
                return;
            }

            if (Object.FindFirstObjectByType<AppController>() != null)
                return; // уже создан (напр. повторная загрузка сцены)

            var go = new GameObject("[Racconotes] App");
            go.AddComponent<AppController>();
        }
    }
}
