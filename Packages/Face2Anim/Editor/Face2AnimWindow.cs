#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class Face2AnimWindow : EditorWindow
{
    [Serializable]
    private struct BlendShapeValue
    {
        public string Name;
        public float Weight;
    }

    private SkinnedMeshRenderer _smr;
    private Transform _avatarRoot;
    private int _blendShapeCount;
    private List<BlendShapeValue> _nonZero = new List<BlendShapeValue>();

    private const float NonZeroEpsilon = Face2AnimExporter.NonZeroEpsilon;

    [MenuItem("Tools/Face2Anim")]
    public static void Open()
    {
        GetWindow<Face2AnimWindow>("Face2Anim");
    }

    private void OnGUI()
    {
        DrawHeader();

        GUILayout.Space(8);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawDropArea();

            GUILayout.Space(10);
            EditorGUILayout.LabelField($"BlendShape: {_nonZero.Count} / {_blendShapeCount}", EditorStyles.boldLabel);

            var sub = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };

            string smrLabel = _smr == null ? "SMR: (未設定)" : $"SMR: {BuildSmrDisplay(_smr)}";
            EditorGUILayout.LabelField(smrLabel, sub);

            GUILayout.Space(10);
            using (new EditorGUI.DisabledScope(_smr == null || _smr.sharedMesh == null || _nonZero.Count == 0))
            {
                if (GUILayout.Button("保存 (.anim)", GUILayout.Height(34)))
                {
                    ExportAnim();
                }
            }
        }
    }

    private void DrawHeader()
    {
        GUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(EditorGUIUtility.IconContent("AnimationClip Icon"), GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label("Face2Anim", EditorStyles.boldLabel);
        }

        var desc = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("今の表情（0以外のBlendShape）を 1フレームの .anim として保存します。", desc);
    }

    private void DrawDropArea()
    {
        const float height = 54f;
        Rect rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));

        var boxStyle = new GUIStyle(EditorStyles.helpBox);
        GUI.Box(rect, "", boxStyle);

        string label = _smr != null
            ? $"Target: {_smr.name}（ここに別の対象をD&Dで差し替え）"
            : "ここに SkinnedMeshRenderer（または GameObject）をドラッグ＆ドロップ";

        var centered = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        EditorGUI.LabelField(rect, label, centered);

        HandleDragAndDrop(rect);
    }

    private void HandleDragAndDrop(Rect rect)
    {
        Event e = Event.current;
        if (!rect.Contains(e.mousePosition))
        {
            return;
        }

        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)
        {
            return;
        }

        var dropped = DragAndDrop.objectReferences;
        if (dropped == null || dropped.Length == 0)
        {
            return;
        }

        var smr = TryResolveSkinnedMeshRenderer(dropped[0]);
        DragAndDrop.visualMode = smr != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

        if (e.type == EventType.DragPerform && smr != null)
        {
            DragAndDrop.AcceptDrag();
            SetTarget(smr);
            e.Use();
        }
        else
        {
            e.Use();
        }
    }

    private static SkinnedMeshRenderer TryResolveSkinnedMeshRenderer(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            return null;
        }

        if (obj is SkinnedMeshRenderer directSmr)
        {
            return directSmr;
        }

        if (obj is GameObject go)
        {
            return go.GetComponent<SkinnedMeshRenderer>() ?? go.GetComponentInChildren<SkinnedMeshRenderer>(true);
        }

        if (obj is Component c)
        {
            return c.GetComponent<SkinnedMeshRenderer>() ?? c.GetComponentInChildren<SkinnedMeshRenderer>(true);
        }

        return null;
    }

    private void SetTarget(SkinnedMeshRenderer smr)
    {
        _smr = smr;
        _avatarRoot = _smr != null ? _smr.transform.root : null;
        RefreshNonZeroList();
        Repaint();
    }

    private void RefreshNonZeroList()
    {
        _nonZero.Clear();
        _blendShapeCount = 0;

        if (_smr == null || _smr.sharedMesh == null)
        {
            return;
        }

        var mesh = _smr.sharedMesh;
        _blendShapeCount = mesh.blendShapeCount;

        for (int i = 0; i < _blendShapeCount; i++)
        {
            float w = _smr.GetBlendShapeWeight(i);
            if (Mathf.Abs(w) <= NonZeroEpsilon)
            {
                continue;
            }

            _nonZero.Add(new BlendShapeValue
            {
                Name = mesh.GetBlendShapeName(i),
                Weight = w
            });
        }
    }

    private void ExportAnim()
    {
        Face2AnimExporter.ExportNonZeroBlendShapesToAnim(_smr);
        RefreshNonZeroList();
    }

    private static string BuildSmrDisplay(SkinnedMeshRenderer smr)
    {
        if (smr == null)
        {
            return "(null)";
        }

        Transform root = smr.transform.root;
        string path = AnimationUtility.CalculateTransformPath(smr.transform, root);
        if (string.IsNullOrEmpty(path))
        {
            return $"{smr.name}  (Root)";
        }

        return $"{smr.name}  ({root.name}/{path})";
    }
}
#endif
