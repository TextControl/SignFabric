using LiteDB;
using Microsoft.Extensions.Options;

namespace SignFabric.Infrastructure.Storage.LiteDb {
    public class LiteDbContext
    {
        public LiteDatabase Database { get; }

        public LiteDbContext(IOptions<LiteDbOptions> options)
        {
            Database = new LiteDatabase(options.Value.DatabaseLocation);
        }
    }

    public class LiteDbOptions
    {
        public string DatabaseLocation { get; set; }
    }
}
