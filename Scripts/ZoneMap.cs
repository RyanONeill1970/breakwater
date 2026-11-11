namespace Breakwater;

/// <summary>
/// The Classic campaign's zone map for the current island: a 2 × 2 grid of zones, blue where a battle
/// has been won, red where the enemy still holds, plain where there is nothing to fight for, and the
/// zone about to be fought outlined, each labelled with its objective.
/// </summary>
public partial class ZoneMap : Control
{
    // Sizes in pixels.
    private const float Gap = 4, Outline = 3;
    private const int LabelSize = 12;
    private static readonly Vector2 LabelInset = new(6, 16);

    private Campaign? campaign;

    public void Show(Campaign shown)
    {
        campaign = shown;
        Visible = shown.Mode == GameMode.Classic;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (campaign == null || campaign.Mode != GameMode.Classic)
        {
            return;
        }

        var cell = (Size - new Vector2(Gap, Gap)) / 2;
        var current = ClassicCampaign.Battle(campaign.Island, campaign.Battle);
        for (int zone = 0; zone < ZoneBattle.ZoneCount; zone++)
        {
            // Zone 0 is south-west; the map is drawn north up.
            var origin = new Vector2(zone % 2 * (cell.X + Gap), (1 - zone / 2) * (cell.Y + Gap));
            var rect = new Rect2(origin, cell);
            bool held = Enumerable.Range(0, campaign.Battle).Any(b => ClassicCampaign.Battle(campaign.Island, b).Zone == zone);
            bool fought = Enumerable.Range(0, ClassicCampaign.BattleCount(campaign.Island)).Any(b => ClassicCampaign.Battle(campaign.Island, b).Zone == zone);
            DrawRect(rect, held ? Palette.Map.HeldZone : fought ? Palette.Map.EnemyZone : Palette.Map.Land);
            if (current.Zone == zone)
            {
                DrawRect(rect, Palette.Ui.Accent, false, Outline);
            }

            var battle = Enumerable.Range(0, ClassicCampaign.BattleCount(campaign.Island))
                .Select(b => ClassicCampaign.Battle(campaign.Island, b))
                .LastOrDefault(b => b.Zone == zone);
            if (battle != null)
            {
                DrawString(UiFonts.Heading, origin + LabelInset, battle.Objective.ToString().ToUpperInvariant(), HorizontalAlignment.Left, cell.X - LabelInset.X, LabelSize, Palette.Ui.Ink);
            }
        }
    }
}
