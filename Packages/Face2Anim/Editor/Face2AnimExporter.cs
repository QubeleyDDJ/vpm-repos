#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Face2Anim と同仕様で、SkinnedMeshRenderer の 0 以外の BlendShape を 1 フレームの .anim に書き出します。
/// </summary>
public static class Face2AnimExporter
{
    public const float NonZeroEpsilon = 0.0001f;

    public static void ExportNonZeroBlendShapesToAnim(SkinnedMeshRenderer smr)
    {
        if (smr == null || smr.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("保存できません", "SkinnedMeshRenderer がセットされていません。", "OK");
            return;
        }

        Mesh mesh = smr.sharedMesh;
        var nonZero = new List<(string name, float weight)>();
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            float w = smr.GetBlendShapeWeight(i);
            if (Mathf.Abs(w) <= NonZeroEpsilon)
                continue;
            nonZero.Add((mesh.GetBlendShapeName(i), w));
        }

        if (nonZero.Count == 0)
        {
            EditorUtility.DisplayDialog("保存できません", "0以外の BlendShape がありません。", "OK");
            return;
        }

        Transform avatarRoot = smr.transform.root;
        if (avatarRoot == null)
        {
            EditorUtility.DisplayDialog("保存できません", "アバタールートを解決できませんでした。", "OK");
            return;
        }

        string defaultFileName = "BaseFace.anim";
        string userPath = EditorUtility.SaveFilePanel(
            "Face2Anim (.anim) を保存",
            Application.dataPath,
            defaultFileName,
            "anim");

        if (string.IsNullOrEmpty(userPath))
            return;

        userPath = userPath.Trim();
        if (!userPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            userPath += ".anim";

        string clipBaseName = SanitizeUnityAssetBaseName(Path.GetFileNameWithoutExtension(userPath));
        if (string.IsNullOrEmpty(clipBaseName))
            clipBaseName = "Face2AnimClip";

        if (File.Exists(userPath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "上書き確認",
                $"次のファイルを上書きしますか？\n\n{userPath}",
                "上書き",
                "キャンセル");
            if (!overwrite)
                return;
        }

        var clip = new AnimationClip
        {
            name = clipBaseName,
            frameRate = 60f
        };

        string relativePath = AnimationUtility.CalculateTransformPath(smr.transform, avatarRoot);
        foreach (var v in nonZero)
        {
            var binding = new EditorCurveBinding
            {
                path = relativePath,
                type = typeof(SkinnedMeshRenderer),
                propertyName = $"blendShape.{v.name}"
            };

            var curve = new AnimationCurve(new Keyframe(0f, v.weight));
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        // 一時アセットのファイル名を「保存名 + GUID」にする（CreateAsset 時に m_Name が temp 名のままになるのを防ぐ）
        string tempAssetPath = $"Assets/Face2Anim/{clipBaseName}_{Guid.NewGuid():N}.anim";
        string fullTemp = Path.Combine(Application.dataPath, tempAssetPath.Substring("Assets/".Length));
        fullTemp = Path.GetFullPath(fullTemp);

        try
        {
            AssetDatabase.CreateAsset(clip, tempAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!File.Exists(fullTemp))
            {
                EditorUtility.DisplayDialog(
                    "保存できません",
                    "一時 .anim の生成に失敗しました。Assets/Face2Anim フォルダが存在するか確認してください。",
                    "OK");
                return;
            }

            string yaml = File.ReadAllText(fullTemp);
            yaml = EnsureYamlAnimationClipName(yaml, clipBaseName);
            File.WriteAllText(userPath, yaml);
        }
        finally
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(tempAssetPath) != null)
                AssetDatabase.DeleteAsset(tempAssetPath);
            AssetDatabase.Refresh();
        }

        string dataNorm = Application.dataPath.Replace('\\', '/').TrimEnd('/');
        string userNorm = Path.GetFullPath(userPath).Replace('\\', '/');
        if (userNorm.StartsWith(dataNorm + "/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(userNorm, dataNorm, StringComparison.OrdinalIgnoreCase))
        {
            string rel = userNorm.Length > dataNorm.Length
                ? "Assets" + userNorm.Substring(dataNorm.Length)
                : "Assets";
            var imported = AssetDatabase.LoadAssetAtPath<AnimationClip>(rel);
            if (imported != null)
            {
                EditorGUIUtility.PingObject(imported);
                Selection.activeObject = imported;
            }
        }
    }

    /// <summary>Windows 等で使えない文字を除き、Unity アセット名として使えるベース名にする。</summary>
    private static string SanitizeUnityAssetBaseName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        string s = new string(chars).Trim();
        if (s.Length > 180)
            s = s.Substring(0, 180);
        return string.IsNullOrEmpty(s) ? null : s;
    }

    /// <summary>最初の <c>m_Name</c> 行（AnimationClip 名）を保存ファイル名と一致させる。</summary>
    private static string EnsureYamlAnimationClipName(string yaml, string clipName)
    {
        if (string.IsNullOrEmpty(yaml) || string.IsNullOrEmpty(clipName))
            return yaml;
        var m = Regex.Match(yaml, @"^(\s*m_Name:\s*).*$", RegexOptions.Multiline, TimeSpan.FromSeconds(2));
        if (!m.Success)
            return yaml;
        return yaml.Remove(m.Index, m.Length).Insert(m.Index, m.Groups[1].Value + clipName);
    }
}
#endif
