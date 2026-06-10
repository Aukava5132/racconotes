using System.Collections.Generic;
using Racconotes.Domain.Entities;

namespace Racconotes.Domain.Repositories
{
    /// <summary>
    /// Контракт доступа к MIDI-трекам и их фильтрации. Реализация — в Infrastructure (Этап 2).
    /// </summary>
    public interface ITrackRepository
    {
        IEnumerable<MidiTrack> GetAllTracks();
        MidiTrack GetTrackById(int id);
        int AddTrack(MidiTrack track);
        void UpdateTrack(MidiTrack track);
        void DeleteTrack(int id);
        IEnumerable<MidiTrack> FilterByDifficulty(float min, float max);
        IEnumerable<MidiTrack> FilterByBpm(int minBpm, int maxBpm);
    }
}
