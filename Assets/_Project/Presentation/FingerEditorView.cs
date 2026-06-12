using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Racconotes.Application;
using Racconotes.Domain.Entities;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Экран редактирования аппликатуры (§2.3, IMGUI, мышь, без TMP — по образцу
    /// <see cref="SettingsView"/>/<see cref="StatsView"/>). Переопределения руки/пальца хранятся в
    /// отдельной таблице UserFingerAssignments (активный компонент БД): исходные Notes не меняются, а
    /// эффективная аппликатура читается через COALESCE
    /// (<see cref="Racconotes.Domain.Repositories.INoteRepository.GetNotesForTrack(int,int)"/>).
    /// Переопределённые ноты подсвечены; «Авто» удаляет переопределение и возвращает значение из Notes.
    /// </summary>
    public sealed class FingerEditorView : MonoBehaviour
    {
        private static readonly Color Accent = new Color(0.30f, 0.65f, 1f);
        private static readonly Color Edited = new Color(1f, 0.82f, 0.28f);

        private GameContext _ctx;
        private int _userId;

        private List<MidiTrack> _tracks = new List<MidiTrack>();
        private int _trackIndex;
        private List<Note> _notes = new List<Note>();             // эффективная аппликатура (COALESCE)
        private HashSet<int> _editedNoteIds = new HashSet<int>();   // ноты с переопределением

        private Vector2 _scroll;
        private GUIStyle _title, _section, _row, _sub, _hint;

        public bool Visible { get; private set; }

        /// <summary>Кнопка «Назад» — вернуться в меню.</summary>
        public event Action OnBack;

        public void Init(GameContext ctx, int userId)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _userId = userId;
        }

        public void Show()
        {
            Visible = true;
            LoadTracks();
            LoadNotes();
        }

        public void Hide() => Visible = false;

        // ----- Данные -----

        private void LoadTracks()
        {
            _tracks = _ctx.TrackRepository.GetAllTracks().ToList();
            if (_trackIndex >= _tracks.Count) _trackIndex = 0;
        }

        private void LoadNotes()
        {
            if (_tracks.Count == 0)
            {
                _notes = new List<Note>();
                _editedNoteIds = new HashSet<int>();
                return;
            }

            int trackId = _tracks[_trackIndex].TrackId;
            _notes = _ctx.NoteRepository.GetNotesForTrack(trackId, _userId).ToList();
            _editedNoteIds = new HashSet<int>(
                _ctx.FingerAssignments.GetForTrack(_userId, trackId).Select(a => a.NoteId));
        }

        private void SetFinger(Note n, int finger)
        {
            _ctx.FingerAssignments.Upsert(new UserFingerAssignment
            {
                UserId = _userId, TrackId = n.TrackId, NoteId = n.NoteId,
                AssignedHand = n.Hand, AssignedFinger = finger
            });
            LoadNotes();
        }

        private void SetHand(Note n, string hand)
        {
            _ctx.FingerAssignments.Upsert(new UserFingerAssignment
            {
                UserId = _userId, TrackId = n.TrackId, NoteId = n.NoteId,
                AssignedHand = hand, AssignedFinger = n.Finger
            });
            LoadNotes();
        }

        private void ResetNote(Note n)
        {
            _ctx.FingerAssignments.Reset(_userId, n.TrackId, n.NoteId);
            LoadNotes();
        }

        private void StepTrack(int delta)
        {
            if (_tracks.Count == 0) return;
            _trackIndex = (_trackIndex + delta + _tracks.Count) % _tracks.Count;
            LoadNotes();
        }

        // ----- Рендер -----

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _section = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
            _row = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            _sub = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Italic };
        }

        private void OnGUI()
        {
            if (!Visible) return;
            EnsureStyles();

            float w = Mathf.Min(900f, Screen.width - 40f);
            float h = Mathf.Min(740f, Screen.height - 40f);
            var box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

            GUI.color = new Color(0f, 0f, 0f, 0.88f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(box.x + 24f, box.y + 18f, w - 48f, h - 36f));

            GUILayout.Label("Аппликатура", _title);
            GUILayout.Space(8f);

            DrawTrackSelector();

            if (_tracks.Count == 0)
                GUILayout.Label("В библиотеке нет треков.", _sub);
            else if (_notes.Count == 0)
                GUILayout.Label("У трека нет нот.", _sub);
            else
                DrawNotes();

            GUILayout.Space(6f);
            if (GUILayout.Button("Назад", GUILayout.Height(40f)))
                OnBack?.Invoke();

            GUILayout.EndArea();
        }

        private void DrawTrackSelector()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◄", GUILayout.Width(44f), GUILayout.Height(32f))) StepTrack(-1);

            string caption = _tracks.Count == 0
                ? "—"
                : $"{_tracks[_trackIndex].Title}   ({_trackIndex + 1}/{_tracks.Count})";
            GUILayout.Label(caption, _section, GUILayout.Height(32f));

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("►", GUILayout.Width(44f), GUILayout.Height(32f))) StepTrack(+1);
            GUILayout.EndHorizontal();

            GUILayout.Label("Палец 1-5 и рука переопределяют авто-аппликатуру и сохраняются в БД. " +
                            "«Авто» возвращает исходное значение. Переопределённые ноты подсвечены.", _hint);
            GUILayout.Space(8f);
        }

        private void DrawNotes()
        {
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (Note n in _notes)
                DrawNoteRow(n);
            GUILayout.EndScrollView();
        }

        private void DrawNoteRow(Note n)
        {
            bool edited = _editedNoteIds.Contains(n.NoteId);

            GUILayout.BeginHorizontal();

            Color prev = GUI.color;
            if (edited) GUI.color = Edited;
            GUILayout.Label($"#{n.NoteIndex + 1}", _row, GUILayout.Width(46f));
            GUILayout.Label(NoteNaming.Name(n.MidiNumber), _row, GUILayout.Width(60f));
            GUILayout.Label($"{n.StartTime:0.0}с", _sub, GUILayout.Width(54f));
            GUI.color = prev;

            // Рука
            if (HandButton("Л", n.Hand == "left") && n.Hand != "left") SetHand(n, "left");
            if (HandButton("П", n.Hand == "right") && n.Hand != "right") SetHand(n, "right");

            GUILayout.Space(10f);

            // Палец 1-5
            for (int f = 1; f <= 5; f++)
                if (FingerButton(f, n.Finger == f) && n.Finger != f) SetFinger(n, f);

            GUILayout.Space(10f);
            if (GUILayout.Button("Авто", GUILayout.Width(70f)))
            {
                if (edited) ResetNote(n);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
        }

        private bool HandButton(string caption, bool active)
        {
            Color prev = GUI.color;
            if (active) GUI.color = Accent;
            bool clicked = GUILayout.Button(caption, GUILayout.Width(40f));
            GUI.color = prev;
            return clicked;
        }

        private bool FingerButton(int finger, bool active)
        {
            Color prev = GUI.color;
            if (active) GUI.color = Accent;
            bool clicked = GUILayout.Button(finger.ToString(), GUILayout.Width(36f));
            GUI.color = prev;
            return clicked;
        }
    }
}
