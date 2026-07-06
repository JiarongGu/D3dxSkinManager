using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607060006)]
public class _202607060006_ReEnrichSparseTagEntries : FluentMigrator.Migration
{
    public override void Up()
    {
        // REPAIR: the sync upsert used to OVERWRITE Tags with the list-page tags (COALESCE takes any
        // non-null excluded value), wiping the detail-page tags enrichment had merged (GameBanana's
        // sub category — the tag the card actually shows). Entries with ≤1 tag get their EnrichedUtc
        // cleared so the next sync's enrichment pass re-fetches their detail page and re-merges.
        // (One-time re-crawl cost for tagless sites like huihui; correct data for GameBanana.)
        Execute.Sql(@"
            UPDATE RemoteIndexEntries
            SET EnrichedUtc = NULL
            WHERE Tags IS NULL OR json_array_length(Tags) <= 1");
    }

    public override void Down()
    {
        // Data repair — nothing to undo.
    }
}
