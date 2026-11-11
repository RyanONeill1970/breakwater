namespace Breakwater;

public partial class QaDriver
{
    private void SceneryCheck()
    {
        foreach (var mode in Enum.GetValues<GameMode>())
        {
            for (int island = 0; island < Mission.Count; island++)
            {
                var campaign = Campaign.Start(mode);
                campaign.Island = island;
                var simulation = new Simulation(campaign);
                var scene = new BattleView();
                AddChild(scene);
                scene.Build(simulation);
                scene.ValidateIslandGeometry();
                scene.Free();
            }
        }
        Pass("SCENERY PASS: all three islands in both modes keep navigable ground level and scanned rocks inside blocking cover.");
    }
}
