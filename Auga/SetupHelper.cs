using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    public static class SetupHelper
    {
        public static bool DirectObjectReplace(Transform original, GameObject prefab, string originalName)
        {
            return DirectObjectReplace(original, prefab, originalName, out _);
        }

        public static bool DirectObjectReplace(Transform original, GameObject prefab, string originalName, out GameObject newObject)
        {
            if (original.name != originalName)
            {
                newObject = null;
                return false;
            }

            var parent = original.parent;
            var siblingIndex = original.GetSiblingIndex();
            var rootCanvas = RootCanvas.Capture(original.gameObject);
            Object.DestroyImmediate(original.gameObject);

            newObject = Object.Instantiate(prefab, parent, false);
            newObject.transform.SetSiblingIndex(siblingIndex);
            rootCanvas?.Wrap(newObject.transform);
            return true;
        }

        /// <summary>
        /// Puts an Auga replacement that sits next to <paramref name="vanilla"/> (instead of destroying it) into a root
        /// canvas set up like the vanilla object's own, if it has one.
        /// </summary>
        public static void WrapInRootCanvasOf(Transform replacement, GameObject vanilla)
        {
            RootCanvas.Capture(vanilla)?.Wrap(replacement);
        }

        /// <summary>
        /// Places two child objects from the prefab in-place at the original, and at a sibling of the original.
        /// Call this method in an Awake prefix, and return !result to avoid calling the Awake of the original.
        /// </summary>
        /// <param name="primaryOriginal">Reference to the original object</param>
        /// <param name="prefab">Prefab that contains two children, one with a different name as the original, but will replace it, and one with the same name as the secondary object</param>
        /// <param name="originalName">The original gameObject name of the object to be replaced</param>
        /// <param name="secondaryName">The name of the secondary gameObject to be replaced. This should be the same in both the original and the new prefab</param>
        /// <param name="newPrimaryName">The name of the object in the prefab that will replace the original. It should be different than the originalName</param>
        /// <returns>true if the objects were replaced (this was called on the original), false otherwise (this was called on the replacement)</returns>
        public static bool IndirectTwoObjectReplace(Transform primaryOriginal, GameObject prefab, string originalName, string secondaryName, string newPrimaryName)
        {
            if (primaryOriginal.name.StartsWith("Auga"))
                return false;

            if (primaryOriginal.name != originalName)
            {
                return false;
            }

            if (!prefab)
            {
                Debug.LogWarning($"[Auga] Prefab for {originalName} converting to {newPrimaryName} for {secondaryName} not found.");
                return false;
            }



            var parent = primaryOriginal.parent;
            if (parent != null)
            {

                var secondaryOriginal = parent.Find(secondaryName);
                if (secondaryOriginal != null)
                {
                    var secondarySiblingIndex = secondaryOriginal.GetSiblingIndex();
                    var primarySiblingIndex = primaryOriginal.GetSiblingIndex();
                    var primaryCanvas = RootCanvas.Capture(primaryOriginal.gameObject);
                    var secondaryCanvas = RootCanvas.Capture(secondaryOriginal.gameObject);

                    Object.DestroyImmediate(secondaryOriginal.gameObject);
                    Object.DestroyImmediate(primaryOriginal.gameObject);

                    var newPrefab = Object.Instantiate(prefab, parent);
                    var secondary = newPrefab.transform.Find(secondaryName);
                    var primary = newPrefab.transform.Find(newPrimaryName);

                    secondary.SetParent(parent);
                    primary.SetParent(parent);
                    secondary.SetSiblingIndex(secondarySiblingIndex);
                    primary.SetSiblingIndex(primarySiblingIndex);
                    primaryCanvas?.Wrap(primary);
                    secondaryCanvas?.Wrap(secondary);

                    Object.Destroy(newPrefab);

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Root canvas set-up of a vanilla object. In 1.0.x every screen under IngameGui (which has no Canvas) is a root
        /// canvas of its own (scene bundle 17245031: DamageText order 100, TopLeftMessage 500, Store_Screen 700,
        /// BarberGui 800, HudMessage 1000, TextViewer 1200); the Auga prefabs were authored as children of one shared
        /// canvas and carry none, so without this they never render (MessageHud's centre text reported canvas=none in
        /// the 21b-build-message autotest). Mirrors upstream RandyKnapp/Auga 79b71af
        /// SetupHelper.CaptureRootCanvas/ApplyRootCanvas; always wrap mode, so each part keeps its authored rect.
        /// </summary>
        private sealed class RootCanvas
        {
            private RenderMode _renderMode;
            private bool _pixelPerfect, _overrideSorting, _hasScaler, _hasRaycaster, _hasGuiScaler, _ignoreReversed;
            private int _sortingOrder, _sortingLayerId;
            private AdditionalCanvasShaderChannels _shaderChannels;
            private CanvasScaler.ScaleMode _scaleMode;
            private CanvasScaler.ScreenMatchMode _matchMode;
            private Vector2 _referenceResolution;
            private float _matchWidthOrHeight, _referencePixelsPerUnit, _scaleFactor;
            private GraphicRaycaster.BlockingObjects _blocking;

            public static RootCanvas Capture(GameObject go)
            {
                var canvas = go.GetComponent<Canvas>();
                if (!canvas)
                    return null;
                var settings = new RootCanvas
                {
                    _renderMode = canvas.renderMode,
                    _pixelPerfect = canvas.pixelPerfect,
                    _overrideSorting = canvas.overrideSorting,
                    _sortingOrder = canvas.sortingOrder,
                    _sortingLayerId = canvas.sortingLayerID,
                    _shaderChannels = canvas.additionalShaderChannels,
                    _hasGuiScaler = go.GetComponent<GuiScaler>()
                };
                var scaler = go.GetComponent<CanvasScaler>();
                if (scaler)
                {
                    settings._hasScaler = true;
                    settings._scaleMode = scaler.uiScaleMode;
                    settings._referenceResolution = scaler.referenceResolution;
                    settings._matchMode = scaler.screenMatchMode;
                    settings._matchWidthOrHeight = scaler.matchWidthOrHeight;
                    settings._referencePixelsPerUnit = scaler.referencePixelsPerUnit;
                    settings._scaleFactor = scaler.scaleFactor;
                }
                var raycaster = go.GetComponent<GraphicRaycaster>();
                if (raycaster)
                {
                    settings._hasRaycaster = true;
                    settings._ignoreReversed = raycaster.ignoreReversedGraphics;
                    settings._blocking = raycaster.blockingObjects;
                }
                return settings;
            }

            // A full-screen wrapper canvas configured like the vanilla one takes the part's place; the part keeps its
            // authored rect inside it (it was laid out as a child of a full-screen canvas).
            public void Wrap(Transform part)
            {
                if (part.GetComponent<Canvas>())
                    return;
                var wrapper = new GameObject(part.name + "Canvas", typeof(RectTransform));
                wrapper.layer = part.gameObject.layer;
                wrapper.transform.SetParent(part.parent, false);
                wrapper.transform.SetSiblingIndex(part.GetSiblingIndex());
                part.SetParent(wrapper.transform, false);

                var canvas = wrapper.AddComponent<Canvas>();
                canvas.renderMode = _renderMode;
                canvas.pixelPerfect = _pixelPerfect;
                canvas.overrideSorting = _overrideSorting;
                canvas.sortingLayerID = _sortingLayerId;
                canvas.sortingOrder = _sortingOrder;
                canvas.additionalShaderChannels = _shaderChannels;
                if (_hasScaler)
                {
                    var scaler = wrapper.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = _scaleMode;
                    scaler.referenceResolution = _referenceResolution;
                    scaler.screenMatchMode = _matchMode;
                    scaler.matchWidthOrHeight = _matchWidthOrHeight;
                    scaler.referencePixelsPerUnit = _referencePixelsPerUnit;
                    scaler.scaleFactor = _scaleFactor;
                }
                if (_hasRaycaster)
                {
                    var raycaster = wrapper.AddComponent<GraphicRaycaster>();
                    raycaster.ignoreReversedGraphics = _ignoreReversed;
                    raycaster.blockingObjects = _blocking;
                }
                if (_hasGuiScaler)
                    wrapper.AddComponent<GuiScaler>();

                var rect = (RectTransform)wrapper.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }
    }
}
