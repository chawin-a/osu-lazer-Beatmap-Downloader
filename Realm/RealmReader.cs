using Realms;

namespace LazerBeatmapLister.Realm;

public static class RealmReader
{
    public static List<int> ReadSetIds(
        string source,
        bool skipLocal)
    {
        string copy =
            Path.Combine(
                Path.GetTempPath(),
                $"client-copy-{Guid.NewGuid():N}.realm");

        File.Copy(source, copy);

        try
        {
            var config =
                new RealmConfiguration(copy)
                {
                    IsReadOnly = true,
                    IsDynamic = true
                };

            using var realm =
                Realms.Realm.GetInstance(config);

            var result =
                new SortedSet<int>();

            foreach (var set in
                     realm.DynamicApi.All("BeatmapSet"))
            {
                if (set.DynamicApi
                    .Get<bool>("DeletePending"))
                {
                    continue;
                }

                int id =
                    set.DynamicApi
                        .Get<int>("OnlineID");

                if (skipLocal && id <= 0)
                    continue;

                result.Add(id);
            }

            return result.ToList();
        }
        finally
        {
            try
            {
                File.Delete(copy);
                File.Delete(copy + ".lock");
            }
            catch
            {
                // Ignore cleanup error.
            }
        }
    }
}
