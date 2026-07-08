using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MimesisFovMod
{
    /// <summary>
    /// Adds a separate, native-looking "FOV" control to the game's Settings screen
    /// (<see cref="UIPrefab_GameSettings"/>) by cloning the existing "Look Sensitivity"
    /// widgets, relabelling the clone and binding its slider to our config.
    ///
    /// The row title carries a localization component that re-applies the translated
    /// "Look Sensitivity" text after we run, so we (a) strip obvious localizer components
    /// from the cloned title and (b) keep forcing the title to "FOV" from LateUpdate as a
    /// bullet-proof fallback (see <see cref="KeepLabel"/>).
    /// </summary>
    [HarmonyPatch]
    internal static class SettingsUiInjector
    {
        private const string RowName    = "FovMod_FovRow";
        private const string LabelName  = "FovMod_FovLabel";
        private const string SliderName = "FovMod_FovSlider";
        private const string LabelText  = "FOV";

        private static bool _dumped;

        /// <summary>The cloned title text; LateUpdate keeps it reading "FOV".</summary>
        private static TMP_Text _fovTitle;

        [HarmonyPatch(typeof(UIPrefab_GameSettings), "OnEnable")]
        [HarmonyPostfix]
        private static void OnEnable_Postfix(UIPrefab_GameSettings __instance)
        {
            UIPrefab_GameSettings ui = __instance; // Harmony injects the instance as "__instance"
            try
            {
                Transform sliderT = ui.UE_LookSensitivity_Slider;
                Slider sens = sliderT != null ? sliderT.GetComponent<Slider>() : null;
                if (sens == null) { FovModPlugin.Log.LogWarning("[FOV] Look Sensitivity slider not found."); return; }
                TMP_Text label = ui.UE_LOOK_SENSITIVITY;

                if (!_dumped)
                {
                    _dumped = true;
                    DumpNeighborhood(sens.transform, label != null ? label.transform : null);
                }

                Inject(sens, label);
            }
            catch (Exception e)
            {
                FovModPlugin.Log.LogError("[FOV] injection failed: " + e);
            }
        }

        /// <summary>Called every LateUpdate: force the FOV title to stay "FOV" against any localizer.</summary>
        public static void KeepLabel()
        {
            if (_fovTitle != null && _fovTitle.text != LabelText)
                _fovTitle.text = LabelText;
        }

        private static void Inject(Slider sens, TMP_Text label)
        {
            Transform sliderT = sens.transform;
            Transform labelT  = label != null ? label.transform : null;

            Transform lca = labelT != null ? Lca(sliderT, labelT) : sliderT.parent;
            int slidersInLca = lca != null ? lca.GetComponentsInChildren<Slider>(true).Length : int.MaxValue;

            if (labelT != null && lca != null && lca != sliderT && slidersInLca == 1)
                InjectAsRow(lca, label);
            else
                InjectAsColumns(sens, label);
        }

        // ---- single compact row: clone the whole row, relabel the title by object name ----
        private static void InjectAsRow(Transform row, TMP_Text srcTitle)
        {
            Transform parent = row.parent;
            if (parent == null) { FovModPlugin.Log.LogWarning("[FOV] row has no parent."); return; }

            Transform existing = parent.Find(RowName);
            if (existing != null) { RefreshValue(existing); RecaptureTitle(existing, srcTitle); return; }

            GameObject clone = UnityEngine.Object.Instantiate(row.gameObject, parent);
            clone.name = RowName;
            clone.transform.SetSiblingIndex(row.GetSiblingIndex() + 1);
            OffsetIfNoLayout(parent, row as RectTransform, clone.transform as RectTransform);

            RelabelClone(clone.transform, srcTitle);
            BindSlider(clone.GetComponentInChildren<Slider>(true));
            FovModPlugin.Log.LogInfo($"[FOV] injected FOV as a cloned row after '{row.name}'.");
        }

        // ---- column layout: clone the label object and the slider object separately ----
        private static void InjectAsColumns(Slider sens, TMP_Text label)
        {
            if (label != null)
            {
                Transform lp = label.transform.parent;
                if (lp != null && lp.Find(LabelName) == null)
                {
                    GameObject lc = UnityEngine.Object.Instantiate(label.gameObject, lp);
                    lc.name = LabelName;
                    lc.transform.SetSiblingIndex(label.transform.GetSiblingIndex() + 1);
                    OffsetIfNoLayout(lp, label.transform as RectTransform, lc.transform as RectTransform);
                    SetFovTitle(lc.GetComponent<TMP_Text>() ?? lc.GetComponentInChildren<TMP_Text>(true));
                }
                else if (lp != null)
                {
                    SetFovTitle(lp.Find(LabelName)?.GetComponent<TMP_Text>());
                }
            }

            Transform sp = sens.transform.parent;
            if (sp == null) { FovModPlugin.Log.LogWarning("[FOV] slider has no parent."); return; }

            Transform existingSlider = sp.Find(SliderName);
            if (existingSlider != null) { RefreshValue(existingSlider); return; }

            GameObject sc = UnityEngine.Object.Instantiate(sens.gameObject, sp);
            sc.name = SliderName;
            sc.transform.SetSiblingIndex(sens.transform.GetSiblingIndex() + 1);
            OffsetIfNoLayout(sp, sens.transform as RectTransform, sc.transform as RectTransform);
            BindSlider(sc.GetComponent<Slider>() ?? sc.GetComponentInChildren<Slider>(true));
            FovModPlugin.Log.LogInfo("[FOV] injected FOV as a separate label + slider (column layout).");
        }

        private static void BindSlider(Slider s)
        {
            if (s == null) { FovModPlugin.Log.LogWarning("[FOV] clone has no Slider to bind."); return; }
            // Replace the whole event so no persistent (inspector) listeners survive on the clone.
            s.onValueChanged = new Slider.SliderEvent();
            s.wholeNumbers = true;
            s.minValue = FovModPlugin.MinFov.Value;
            s.maxValue = FovModPlugin.MaxFov.Value;
            s.SetValueWithoutNotify(Mathf.Clamp(FovModPlugin.Fov.Value, s.minValue, s.maxValue));
            s.onValueChanged.AddListener(OnFovSliderChanged);
        }

        private static void OnFovSliderChanged(float value)
        {
            FovModPlugin.Fov.Value = value; // Fov.SettingChanged -> CameraFov.Apply()
        }

        private static void RefreshValue(Transform node)
        {
            Slider s = node.GetComponent<Slider>() ?? node.GetComponentInChildren<Slider>(true);
            if (s == null) return;
            s.minValue = FovModPlugin.MinFov.Value;
            s.maxValue = FovModPlugin.MaxFov.Value;
            s.SetValueWithoutNotify(Mathf.Clamp(FovModPlugin.Fov.Value, s.minValue, s.maxValue));
        }

        /// <summary>Find the cloned title by object name, strip localizers, set "FOV".</summary>
        private static void RelabelClone(Transform cloneRoot, TMP_Text srcTitle)
        {
            TMP_Text target = null;
            if (srcTitle != null)
            {
                foreach (TMP_Text t in cloneRoot.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.name == srcTitle.name) { target = t; break; }
                }
            }
            if (target == null)
            {
                TMP_Text[] all = cloneRoot.GetComponentsInChildren<TMP_Text>(true);
                if (all.Length > 0) target = all[0];
            }
            SetFovTitle(target);
        }

        private static void RecaptureTitle(Transform cloneRow, TMP_Text srcTitle)
        {
            if (_fovTitle != null) return;
            RelabelClone(cloneRow, srcTitle);
        }

        private static void SetFovTitle(TMP_Text title)
        {
            if (title == null) { FovModPlugin.Log.LogWarning("[FOV] could not find a title text to relabel."); return; }
            StripLocalizers(title.gameObject);
            title.text = LabelText;
            _fovTitle = title;
        }

        /// <summary>Remove components that would re-apply a localized string over our "FOV".</summary>
        private static void StripLocalizers(GameObject go)
        {
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null || c is Transform || c is TMP_Text) continue;
                string n = c.GetType().Name.ToLowerInvariant();
                if (n.Contains("local") || n.Contains("l10n") || n.Contains("lang") || n.Contains("translat"))
                    UnityEngine.Object.Destroy(c);
            }
        }

        /// <summary>Push the clone below the source when the parent has no LayoutGroup to place it.</summary>
        private static void OffsetIfNoLayout(Transform parent, RectTransform src, RectTransform clone)
        {
            if (parent == null || src == null || clone == null) return;
            if (parent.GetComponent<LayoutGroup>() != null) return;

            float spacing = Mathf.Max(src.rect.height, 1f);
            int idx = src.GetSiblingIndex();
            for (int i = 0; i < parent.childCount; i++)
            {
                if (i == idx) continue;
                RectTransform rt = parent.GetChild(i) as RectTransform;
                if (rt == null) continue;
                float dy = src.anchoredPosition.y - rt.anchoredPosition.y;
                if (dy > 1f && dy < spacing * 3f) { spacing = dy; break; }
            }
            clone.anchoredPosition = src.anchoredPosition + new Vector2(0f, -spacing);
        }

        private static Transform Lca(Transform a, Transform b)
        {
            var set = new HashSet<Transform>();
            for (Transform t = a; t != null; t = t.parent) set.Add(t);
            for (Transform t = b; t != null; t = t.parent) if (set.Contains(t)) return t;
            return null;
        }

        // ---------- diagnostics ----------

        private static void DumpNeighborhood(Transform slider, Transform label)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[FOV] ===== settings hierarchy dump =====");
            sb.AppendLine("[FOV] slider path: " + Path(slider));
            sb.AppendLine("[FOV] label  path: " + (label != null ? Path(label) : "<null>"));
            Transform lca = label != null ? Lca(slider, label) : slider.parent;
            sb.AppendLine("[FOV] LCA    path: " + (lca != null ? Path(lca) : "<null>"));
            if (lca != null)
                sb.AppendLine($"[FOV] LCA sliders={lca.GetComponentsInChildren<Slider>(true).Length}, " +
                              $"parentLayoutGroup={(lca.parent != null && lca.parent.GetComponent<LayoutGroup>() != null)}");

            Transform root = lca != null ? lca : slider.parent;
            int lines = 0;
            DumpTree(root, 0, sb, ref lines);
            FovModPlugin.Log.LogInfo(sb.ToString());
        }

        private static void DumpTree(Transform t, int depth, StringBuilder sb, ref int lines)
        {
            if (t == null || lines > 250 || depth > 6) return;
            lines++;
            TMP_Text txt = t.GetComponent<TMP_Text>();
            string comps = string.Join(",", t.GetComponents<Component>()
                .Where(c => c != null && !(c is Transform))
                .Select(c => c.GetType().Name));
            string info = "";
            if (txt != null) info += " text=\"" + Trunc(txt.text) + "\"";
            sb.AppendLine("[FOV] " + new string(' ', depth * 2) + t.name + "  {" + comps + "}" + info);
            for (int i = 0; i < t.childCount; i++) DumpTree(t.GetChild(i), depth + 1, sb, ref lines);
        }

        private static string Trunc(string s) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length > 24 ? s.Substring(0, 24) : s);

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
