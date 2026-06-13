using Racconotes.Domain.Entities;

namespace Racconotes.Domain.Repositories
{
    /// <summary>
    /// Контракт доступа к настройкам пользователя (таблица UserSettings, §2.1). Реализация — в Infrastructure.
    /// Делает таблицу настроек активным компонентом: меню настроек читает и сохраняет режимы подписи.
    /// </summary>
    public interface IUserSettingsRepository
    {
        /// <summary>Настройки пользователя или <c>null</c>, если строки ещё нет.</summary>
        UserSettings GetSettings(int userId);

        /// <summary>Сохранить настройки (вставка или обновление по user_id).</summary>
        void SaveSettings(UserSettings settings);
    }
}
