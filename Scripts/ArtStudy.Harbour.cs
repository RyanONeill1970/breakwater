namespace Breakwater;

public partial class ArtStudy
{
    private static readonly Vector3 QuayOrigin = new(-84, 2.3f, 11);

    private void BuildDetailedHarbour()
    {
        IslandHarbour.Build(this, QuayOrigin);
        BuildHarbourRoadside(HarbourMaterial(Palette.Art.Concrete, .12f), HarbourMaterial(Palette.Art.Coping, .3f), HarbourMaterial(Palette.Art.Metal, .6f));
    }

    private static Material HarbourMaterial(Color color, float grainScale) => IslandHarbour.HarbourMaterial(color, grainScale);
    private void BuildHarbourRoadside(Material concrete, Material coping, Material metal)
    {
        var batch = new ArtStudyBatch();
        for (int i = 0; i < 12; i++)
        {
            var p = new Vector2(-61 + i * 2.2f, 28 + i * 1.05f);
            var ground = Ground(p);
            batch.Box(ground + Vector3.Up * .55f, new(2.25f, 1.1f, .7f), concrete, -.44f);
            batch.Box(ground + Vector3.Up * 1.16f, new(2.3f, .14f, .86f), coping, -.44f);
            if (i % 3 == 0)
            {
                batch.Box(ground + Vector3.Up * .8f, new(.7f, 1.6f, .9f), concrete, -.44f);
            }
        }
        foreach (var p in (Vector2[])[new(-65, 22), new(-57, -2)])
        {
            var basePoint = Ground(p);
            batch.Beam(basePoint, basePoint + Vector3.Up * 7, .12f, metal);
            batch.Beam(basePoint + Vector3.Up * 7, basePoint + new Vector3(-1.5f, 7.5f, 0), .08f, metal);
            batch.Box(basePoint + new Vector3(-1.5f, 7.4f, 0), new(.7f, .2f, .45f), metal);
        }
        batch.Commit(this);
    }
}
