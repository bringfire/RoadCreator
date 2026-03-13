using global::Rhino;
using global::Rhino.DocObjects;
using System.Drawing;

namespace RoadCreator.Rhino.Layers;

/// <summary>
/// Manages layer creation and hierarchy for RoadCreator.
/// Replaces the repetitive IsLayer/AddLayer/CurrentLayer pattern from every VBScript file.
/// </summary>
public class LayerManager
{
    private readonly RhinoDoc _doc;

    public LayerManager(RhinoDoc doc)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// Ensure a layer exists, creating it (and any parent layers) if needed.
    /// Supports "::" separated hierarchical paths.
    /// Returns the layer index.
    /// </summary>
    public int EnsureLayer(string fullPath, Color? color = null)
    {
        int existingIndex = _doc.Layers.FindByFullPath(fullPath, -1);
        if (existingIndex >= 0)
            return existingIndex;

        // Split path and create hierarchy
        var parts = fullPath.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
        int parentIndex = -1;
        string builtPath = "";

        for (int i = 0; i < parts.Length; i++)
        {
            builtPath = i == 0 ? parts[i] : builtPath + "::" + parts[i];

            int idx = _doc.Layers.FindByFullPath(builtPath, -1);
            if (idx >= 0)
            {
                parentIndex = idx;
                continue;
            }

            var layer = new Layer
            {
                Name = parts[i],
            };

            if (parentIndex >= 0)
                layer.ParentLayerId = _doc.Layers[parentIndex].Id;

            // Apply color only to the final (leaf) layer
            if (i == parts.Length - 1 && color.HasValue)
                layer.Color = color.Value;

            parentIndex = _doc.Layers.Add(layer);
        }

        return parentIndex;
    }

    /// <summary>
    /// Ensure a layer exists and set it as the current layer.
    /// Returns the layer index.
    /// </summary>
    public int SetCurrentLayer(string fullPath, Color? color = null)
    {
        int index = EnsureLayer(fullPath, color);
        _doc.Layers.SetCurrentLayerIndex(index, true);
        return index;
    }

    /// <summary>
    /// Lock a layer if it exists.
    /// </summary>
    public void LockLayer(string fullPath)
    {
        int index = _doc.Layers.FindByFullPath(fullPath, -1);
        if (index >= 0)
        {
            var layer = _doc.Layers[index];
            layer.IsLocked = true;
            _doc.Layers.Modify(layer, index, true);
        }
    }

    /// <summary>
    /// Set layer visibility.
    /// </summary>
    public void SetLayerVisible(string fullPath, bool visible)
    {
        int index = _doc.Layers.FindByFullPath(fullPath, -1);
        if (index >= 0)
        {
            var layer = _doc.Layers[index];
            layer.IsVisible = visible;
            _doc.Layers.Modify(layer, index, true);
        }
    }

    /// <summary>
    /// Ensure a RoadCreator layer under the root.
    /// Shorthand for EnsureLayer(LayerScheme.BuildPath(...)).
    /// </summary>
    public int EnsureRoadCreatorLayer(string layerName, Color? color = null)
    {
        return EnsureLayer(LayerScheme.BuildPath(layerName), color);
    }

    /// <summary>
    /// Set a RoadCreator layer as current.
    /// </summary>
    public int SetCurrentRoadCreatorLayer(string layerName, Color? color = null)
    {
        return SetCurrentLayer(LayerScheme.BuildPath(layerName), color);
    }
}
