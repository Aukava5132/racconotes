using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Racconotes.Application;
using Racconotes.Domain.Entities;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Экран настроек (IMGUI, мышь, без TMP — по образцу <see cref="StatsView"/>). Три задачи:
    /// 1) выбор режима подписи клавиш и летящих нот (Кнопки/Ноты/Сольфеджио/Выкл, независимо) —
    ///    хранится в БД (<see cref="GameContext.UserSettingsRepository"/>, таблица UserSettings как
    ///    активный компонент); 2) удаление треков из библиотеки (каскадно удаляет ноты по FK);
    /// 3) импорт пользовательского MIDI из папки <c>persistentDataPath/Import</c>
    ///    (<see cref="GameContext.MidiImport"/>). Об изменениях библиотеки сообщает наружу
    ///    через <see cref="OnLibraryChanged"/>, чтобы меню перечитало список.
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        private static readonly Color Accent = new Color(0.30f, 0.65f, 1f);
        private static readonly Color Danger = new Color(1f, 0.45f, 0.45f);

        // Порядок кнопок режима в строке (Off последним — как «выключить»).
        private static readonly LabelMode[] ModeOrder =
            { LabelMode.KeyboardKey, LabelMode.NoteName, LabelMode.Solfege, LabelMode.Off };

        private GameContext _ctx;
        private int _userId;

        private LabelMode _keyMode = LabelMode.Off;
        private LabelMode _noteMode = LabelMode.Off;

        private List<MidiTrack> _tracks = new List<MidiTrack>();
        private List<string> _importFiles = new List<string>();
        private int _confirmDeleteTrackId = -1;
        private string _message;

        private Vector2 _scroll;
        private GUIStyle _title, _section, _row, _sub, _path, _hint;

        public bool Visible { get; private set; }

        /// <summary>Кнопка «Назад» — вернуться в меню.</summary>
        public event Action OnBack;

        /// <summary>Библиотека изменилась (импорт/удаление) — меню должно перечитать список.</summary>
        public event Action OnLibraryChanged;

        // UnityEngine.Application указано полностью: голое Application неоднозначно с namespace Racconotes.Application.
        private static string ImportDir => Path.Combine(UnityEngine.Application.persistentDataPath, "Import");

        public void Init(GameContext ctx, int userId)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _userId = userId;
        }

        public void Show()
        {
            Visible = true;
            _message = null;
            _confirmDeleteTrackId = -1;
            LoadSettings();
            LoadTracks();
            ScanImportFolder();
        }

        public void Hide() => Visible = false;

        // ----- Данные -----

        private void LoadSettings()
        {
            UserSettings s = _ctx.UserSettingsRepository.GetSettings(_userId);
            _keyMode = LabelModeCodec.FromDbString(s?.KeyLabelMode);
            _noteMode = LabelModeCodec.FromDbString(s?.NoteLabelMode);
        }

        private void SaveSettings()
        {
            UserSettings s = _ctx.UserSettingsRepository.GetSettings(_userId) ?? new UserSettings { UserId = _userId };
            s.KeyLabelMode = LabelModeCodec.ToDbString(_keyMode);
            s.NoteLabelMode = LabelModeCodec.ToDbString(_noteMode);
            _ctx.UserSettingsRepository.SaveSettings(s);
        }

        private void LoadTracks() => _tracks = _ctx.TrackRepository.GetAllTracks().ToList();

        private void ScanImportFolder()
        {
            try
            {
                if (!Directory.Exists(ImportDir)) Directory.CreateDirectory(ImportDir);
                _importFiles = Directory.GetFiles(ImportDir)
                    .Where(IsMidi)
                    .OrderBy(Path.GetFileName)
                    .ToList();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Racconotes] Не удалось прочитать папку импорта: {e.Message}");
                _importFiles = new List<string>();
            }
        }

        private static bool IsMidi(string file)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            return ext == ".mid" || ext == ".midi";
        }

        private void ImportFile(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                int trackId = _ctx.MidiImport.ImportFromBytes(bytes, Path.GetFileName(path));
                Debug.Log($"[Racconotes] Импортирован «{Path.GetFileName(path)}» → track_id={trackId}.");
                _message = $"Импортирован: {Path.GetFileName(path)} (id={trackId}).";
                LoadTracks();
                ScanImportFolder();
                OnLibraryChanged?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Racconotes] Ошибка импорта «{Path.GetFileName(path)}»: {e.Message}");
                _message = $"Ошибка импорта «{Path.GetFileName(path)}»: {e.Message}";
            }
        }

        private void DeleteTrack(int trackId)
        {
            try
            {
                _ctx.TrackRepository.DeleteTrack(trackId); // FK ON DELETE CASCADE удалит ноты
                Debug.Log($"[Racconotes] Удалён трек {trackId}.");
                _message = $"Трек {trackId} удалён.";
                LoadTracks();
                OnLibraryChanged?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Racconotes] Ошибка удаления трека {trackId}: {e.Message}");
                _message = $"Ошибка удаления: {e.Message}";
            }
            _confirmDeleteTrackId = -1;
        }

        // ----- Рендер -----

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _section = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
            _row = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _sub = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _path = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Italic };
        }

        private void OnGUI()
        {
            if (!Visible) return;
            EnsureStyles();

            float w = Mathf.Min(840f, Screen.width - 40f);
            float h = Mathf.Min(720f, Screen.height - 40f);
            var box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

            GUI.color = new Color(0f, 0f, 0f, 0.88f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(box.x + 24f, box.y + 18f, w - 48f, h - 36f));

            GUILayout.Label("Настройки", _title);
            GUILayout.Space(8f);

            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawLabelsSection();
            DrawLibrarySection();
            DrawImportSection();
            GUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_message))
            {
                GUILayout.Space(4f);
                GUILayout.Label(_message, _hint);
            }

            GUILayout.Space(6f);
            if (GUILayout.Button("Назад", GUILayout.Height(40f)))
                OnBack?.Invoke();

            GUILayout.EndArea();
        }

        private void DrawLabelsSection()
        {
            GUILayout.Label("Подписи при игре", _section);

            LabelMode newKey = ModeRow("Клавиши:", _keyMode);
            if (newKey != _keyMode) { _keyMode = newKey; SaveSettings(); }

            LabelMode newNote = ModeRow("Летящие ноты:", _noteMode);
            if (newNote != _noteMode) { _noteMode = newNote; SaveSettings(); }

            GUILayout.Label("На белых клавишах текст чёрный, на чёрных — белый. " +
                            "«Кнопки» подписывает только ноты в пределах двух октав раскладки.", _hint);
            GUILayout.Space(14f);
        }

        private LabelMode ModeRow(string caption, LabelMode current)
        {
            LabelMode result = current;
            GUILayout.BeginHorizontal();
            GUILayout.Label(caption, _row, GUILayout.Width(170f));
            foreach (LabelMode m in ModeOrder)
            {
                bool active = current == m;
                Color prev = GUI.color;
                if (active) GUI.color = Accent;
                bool clicked = GUILayout.Button(LabelModeCodec.RuName(m), GUILayout.Width(140f), GUILayout.Height(32f));
                GUI.color = prev;
                if (clicked && !active) result = m;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return result;
        }

        private void DrawLibrarySection()
        {
            GUILayout.Label("Библиотека треков", _section);
            if (_tracks.Count == 0)
            {
                GUILayout.Label("Треков нет.", _sub);
                GUILayout.Space(14f);
                return;
            }

            foreach (MidiTrack t in _tracks)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{t.Title}   ·   BPM {t.Bpm:0}   ·   сложность {t.Difficulty:0.0}", _sub);
                GUILayout.FlexibleSpace();

                if (_confirmDeleteTrackId == t.TrackId)
                {
                    GUILayout.Label("Удалить?", _sub, GUILayout.Width(80f));
                    Color prev = GUI.color;
                    GUI.color = Danger;
                    if (GUILayout.Button("Да", GUILayout.Width(70f))) DeleteTrack(t.TrackId);
                    GUI.color = prev;
                    if (GUILayout.Button("Отмена", GUILayout.Width(90f))) _confirmDeleteTrackId = -1;
                }
                else
                {
                    if (GUILayout.Button("Удалить", GUILayout.Width(110f)))
                        _confirmDeleteTrackId = t.TrackId;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(14f);
        }

        private void DrawImportSection()
        {
            GUILayout.Label("Импорт MIDI", _section);
            GUILayout.Label("Положите .mid/.midi файлы в эту папку, затем обновите список:", _sub);
            GUILayout.Label(ImportDir, _path);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Открыть папку Import", GUILayout.Width(220f), GUILayout.Height(32f)))
            {
                ScanImportFolder(); // гарантирует существование папки
                UnityEngine.Application.OpenURL("file://" + ImportDir);
            }
            if (GUILayout.Button("Обновить список", GUILayout.Width(180f), GUILayout.Height(32f)))
                ScanImportFolder();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            if (_importFiles.Count == 0)
            {
                GUILayout.Label("В папке нет MIDI-файлов.", _hint);
            }
            else
            {
                foreach (string file in _importFiles)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Path.GetFileName(file), _sub);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Импортировать", GUILayout.Width(150f)))
                        ImportFile(file);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.Space(4f);
        }
    }
}
