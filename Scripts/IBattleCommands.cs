namespace Breakwater;

using V2 = System.Numerics.Vector2;

/// <summary>A deployment-screen action on one group.</summary>
public enum DeployAction
{
    Refit,
    Repair,
    Replace,
}

/// <summary>What the HUD's buttons and minimap ask the battle controller to do.</summary>
public interface IBattleCommands
{
    /// <summary>Classic: readies an artillery mission (each press changes the shell, then cancels); the next ground order fires it.</summary>
    void Artillery();

    void Configure(int group, DeployAction action);

    /// <summary>Gunner sight: loads the selected leader's next ammunition type.</summary>
    void CycleAmmunition();

    void ContinueCampaign();

    /// <summary>Gunner sight: fires the selected leader's loaded round along its gun.</summary>
    void FireGunner();

    void CycleSaveSlot();
    void Deploy();
    /// <summary>Classic: the selected group's leader lays a mine.</summary>
    void LayMine();

    void Load();
    void LoadAutosave();

    /// <summary>A minimap click: right-click orders the selected group there, left-click focuses the camera.</summary>
    void MapClick(V2 point, bool right);

    /// <summary>Shows a group window's view full screen.</summary>
    void Maximise(int group, WindowMode mode);

    void NewCampaign();

    /// <summary>Starts a new campaign under the other rule set (Modern or Classic).</summary>
    void SwitchMode();

    void OpenSettings();

    /// <summary>Orders <paramref name="group"/> to <paramref name="point"/>, from a group window's map.</summary>
    void OrderGroup(int group, V2 point);

    /// <summary>Closes the game. Progress up to the last deployment is in the autosave.</summary>
    void Quit();

    void Regroup();
    void Restart();
    void Save();
    void SelectGroup(int group);

    /// <summary>Classic: the selected group fires smoke.</summary>
    void Smoke();

    void Stop();
    void ToggleFire();
    void ToggleMute();
    void TogglePause();
    void ToggleView();
}
