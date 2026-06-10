using System.Collections.Generic;
using System.Linq;
using Racconotes.Domain.Entities;

namespace Racconotes.Presentation
{
    /// <summary>Ключ сортировки списка треков в меню выбора.</summary>
    public enum TrackSortKey { Title, Bpm, Difficulty }

    /// <summary>
    /// Чистая (без рендера) логика меню выбора трека: объединение двух SQL-выборок
    /// (<see cref="Racconotes.Domain.Repositories.ITrackRepository.FilterByBpm"/> и
    /// <see cref="Racconotes.Domain.Repositories.ITrackRepository.FilterByDifficulty"/>) в фильтр
    /// «И-И» и презентационная сортировка. Сам фильтр выполняет СУБД (БД — активный компонент),
    /// а здесь только пересечение результатов по TrackId и упорядочивание для показа.
    /// </summary>
    public static class TrackListing
    {
        /// <summary>
        /// Пересечение двух выборок по <see cref="MidiTrack.TrackId"/>: трек попадает в результат,
        /// только если присутствует в обеих (фильтр по BPM И по сложности одновременно).
        /// Порядок берётся из первой выборки, дубликаты по TrackId схлопываются.
        /// </summary>
        public static IReadOnlyList<MidiTrack> Intersect(
            IEnumerable<MidiTrack> byBpm, IEnumerable<MidiTrack> byDifficulty)
        {
            var allowed = new HashSet<int>(byDifficulty.Select(t => t.TrackId));
            var seen = new HashSet<int>();
            var result = new List<MidiTrack>();

            foreach (MidiTrack track in byBpm)
            {
                if (allowed.Contains(track.TrackId) && seen.Add(track.TrackId))
                    result.Add(track);
            }

            return result;
        }

        /// <summary>
        /// Упорядочить треки по выбранному ключу. Сортировка устойчивая; при равенстве ключа
        /// — по названию (Title), чтобы порядок был детерминированным.
        /// </summary>
        public static IReadOnlyList<MidiTrack> Sort(
            IEnumerable<MidiTrack> tracks, TrackSortKey key, bool ascending)
        {
            IOrderedEnumerable<MidiTrack> ordered;
            switch (key)
            {
                case TrackSortKey.Bpm:
                    ordered = ascending
                        ? tracks.OrderBy(t => t.Bpm)
                        : tracks.OrderByDescending(t => t.Bpm);
                    break;
                case TrackSortKey.Difficulty:
                    ordered = ascending
                        ? tracks.OrderBy(t => t.Difficulty)
                        : tracks.OrderByDescending(t => t.Difficulty);
                    break;
                default: // Title
                    ordered = ascending
                        ? tracks.OrderBy(t => t.Title)
                        : tracks.OrderByDescending(t => t.Title);
                    break;
            }

            return ordered.ThenBy(t => t.Title).ToList();
        }
    }
}