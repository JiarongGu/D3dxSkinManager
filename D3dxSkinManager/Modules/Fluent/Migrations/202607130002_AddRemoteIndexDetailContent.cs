using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607130002)]
public class _202607130002_AddRemoteIndexDetailContent : FluentMigrator.Migration
{
    public override void Up()
    {
        // Persisted DETAIL content (gallery images + download options + description, serialized as JSON)
        // so the detail screen can fall back to a cached copy when a LIVE re-fetch fails (site down,
        // scraping blocked, offline) — the "live-first, cache-fallback" contract. DetailFetchedUtc = when
        // the content was last persisted. Detail TAGS already live in the Tags column (enrichment merges
        // them); this stores the REST of the detail page.
        Alter.Table("RemoteIndexEntries")
            .AddColumn("DetailJson").AsString().Nullable()
            .AddColumn("DetailFetchedUtc").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Column("DetailJson").Column("DetailFetchedUtc").FromTable("RemoteIndexEntries");
    }
}
