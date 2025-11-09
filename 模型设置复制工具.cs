using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;

public class CopyModelSettingsWindow : EditorWindow
{
    GameObject sourcePrefab;
    GameObject targetPrefab;

    // 选项
    bool copyTransform;
    bool copyAnimator;
    bool copyMaterials;
    bool copyBlendShapes;
    bool copyVRCDescriptor;
    bool copyDynamicBones;
    bool copyAllComponents;

    Vector2 scroll;

    const string PREF_PREFIX = "CopyModelSettings_";

    [MenuItem("Tools/模型设置复制工具")]
    public static void ShowWindow()
    {
        GetWindow<CopyModelSettingsWindow>("模型设置复制工具");
    }

    void OnEnable()
    {
        copyTransform = EditorPrefs.GetBool(PREF_PREFIX + "Transform", true);
        copyAnimator = EditorPrefs.GetBool(PREF_PREFIX + "Animator", true);
        copyMaterials = EditorPrefs.GetBool(PREF_PREFIX + "Materials", true);
        copyBlendShapes = EditorPrefs.GetBool(PREF_PREFIX + "BlendShapes", true);
        copyVRCDescriptor = EditorPrefs.GetBool(PREF_PREFIX + "VRCDescriptor", true);
        copyDynamicBones = EditorPrefs.GetBool(PREF_PREFIX + "DynamicBones", true);
        copyAllComponents = EditorPrefs.GetBool(PREF_PREFIX + "AllComponents", false);
    }

    void OnDisable()
    {
        EditorPrefs.SetBool(PREF_PREFIX + "Transform", copyTransform);
        EditorPrefs.SetBool(PREF_PREFIX + "Animator", copyAnimator);
        EditorPrefs.SetBool(PREF_PREFIX + "Materials", copyMaterials);
        EditorPrefs.SetBool(PREF_PREFIX + "BlendShapes", copyBlendShapes);
        EditorPrefs.SetBool(PREF_PREFIX + "VRCDescriptor", copyVRCDescriptor);
        EditorPrefs.SetBool(PREF_PREFIX + "DynamicBones", copyDynamicBones);
        EditorPrefs.SetBool(PREF_PREFIX + "AllComponents", copyAllComponents);
    }

    void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("模型设置复制工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("从上方源模型复制所选设置到下方目标模型。", MessageType.Info);
        EditorGUILayout.Space(8);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("源模型 (Source Prefab)", EditorStyles.boldLabel);
        sourcePrefab = (GameObject)EditorGUILayout.ObjectField(sourcePrefab, typeof(GameObject), false);

        EditorGUILayout.LabelField("目标模型 (Target Prefab)", EditorStyles.boldLabel);
        targetPrefab = (GameObject)EditorGUILayout.ObjectField(targetPrefab, typeof(GameObject), false);

        DrawSeparator();
        EditorGUILayout.LabelField("选择要复制的设置", EditorStyles.boldLabel);

        copyTransform = EditorGUILayout.ToggleLeft("Transform（位置、旋转、缩放）", copyTransform);
        copyAnimator = EditorGUILayout.ToggleLeft("Animator（控制器、Avatar）", copyAnimator);
        copyMaterials = EditorGUILayout.ToggleLeft("Renderer 材质", copyMaterials);
        copyBlendShapes = EditorGUILayout.ToggleLeft("BlendShape 权重", copyBlendShapes);
        copyVRCDescriptor = EditorGUILayout.ToggleLeft("VRC Avatar Descriptor", copyVRCDescriptor);
        copyDynamicBones = EditorGUILayout.ToggleLeft("DynamicBone / PhysBone", copyDynamicBones);
        copyAllComponents = EditorGUILayout.ToggleLeft("所有其他组件", copyAllComponents);

        EditorGUILayout.EndScrollView();
        DrawSeparator();

        GUI.enabled = sourcePrefab && targetPrefab;
        if (GUILayout.Button("复制设置", GUILayout.Height(35)))
        {
            CopySettings();
        }
        GUI.enabled = true;
    }

    void DrawSeparator()
    {
        EditorGUILayout.Space(6);
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
        EditorGUILayout.Space(6);
    }

    void CopySettings()
    {
        if (!sourcePrefab || !targetPrefab)
        {
            EditorUtility.DisplayDialog("提示", "请先选择源模型和目标模型。", "确定");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(targetPrefab, "Copy Model Settings");

        int copied = 0;
        var src = sourcePrefab;
        var dst = targetPrefab;

        try
        {
            if (copyTransform)
            {
                dst.transform.localPosition = src.transform.localPosition;
                dst.transform.localRotation = src.transform.localRotation;
                dst.transform.localScale = src.transform.localScale;
                copied++;
            }

            if (copyAnimator)
            {
                var srcAnim = src.GetComponent<Animator>();
                var dstAnim = dst.GetComponent<Animator>();
                if (srcAnim && dstAnim)
                {
                    dstAnim.runtimeAnimatorController = srcAnim.runtimeAnimatorController;
                    dstAnim.avatar = srcAnim.avatar;
                    copied++;
                }
            }

            if (copyMaterials || copyBlendShapes)
            {
                var srcRenderers = src.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var dstRenderers = dst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < Mathf.Min(srcRenderers.Length, dstRenderers.Length); i++)
                {
                    var sR = srcRenderers[i];
                    var dR = dstRenderers[i];

                    if (copyMaterials)
                        dR.sharedMaterials = sR.sharedMaterials;

                    if (copyBlendShapes && sR.sharedMesh != null && dR.sharedMesh != null)
                    {
                        int count = Math.Min(sR.sharedMesh.blendShapeCount, dR.sharedMesh.blendShapeCount);
                        for (int j = 0; j < count; j++)
                        {
                            string name = sR.sharedMesh.GetBlendShapeName(j);
                            int targetIndex = FindBlendShapeIndexByName(dR.sharedMesh, name);
                            if (targetIndex >= 0)
                                dR.SetBlendShapeWeight(targetIndex, sR.GetBlendShapeWeight(j));
                        }
                    }
                }
                copied++;
            }

            if (copyVRCDescriptor)
            {
#if VRC_SDK_VRCSDK3
                var srcDesc = src.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                var dstDesc = dst.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                if (srcDesc && dstDesc)
                {
                    EditorUtility.CopySerialized(srcDesc, dstDesc);
                    copied++;
                }
#endif
            }

            if (copyDynamicBones)
            {
                var srcBones = src.GetComponentsInChildren<Component>(true)
                    .Where(c => c && (c.GetType().Name.Contains("DynamicBone") || c.GetType().Name.Contains("PhysBone")));
                foreach (var srcBone in srcBones)
                {
                    var path = GetRelativePath(src.transform, srcBone.transform);
                    var dstBoneObj = dst.transform.Find(path);
                    if (dstBoneObj)
                    {
                        var dstBone = dstBoneObj.GetComponent(srcBone.GetType());
                        if (dstBone)
                        {
                            EditorUtility.CopySerialized(srcBone, dstBone);
                            copied++;
                        }
                    }
                }
            }

            if (copyAllComponents)
            {
                CopyAllComponents(src, dst);
                copied++;
            }

            Debug.Log($"✅ 成功复制 {copied} 项设置（包括 BlendShape 如已选）。");
            EditorUtility.DisplayDialog("完成", $"已复制 {copied} 项设置。", "确定");
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ 复制出错：{ex.Message}");
        }
    }

    void CopyAllComponents(GameObject src, GameObject dst)
    {
        var srcComps = src.GetComponents<Component>();
        foreach (var srcComp in srcComps)
        {
            if (!srcComp || srcComp is Transform) continue;
            var dstComp = dst.GetComponent(srcComp.GetType());
            if (!dstComp)
                dstComp = dst.AddComponent(srcComp.GetType());
            EditorUtility.CopySerialized(srcComp, dstComp);
        }

        // 递归子物体
        for (int i = 0; i < src.transform.childCount; i++)
        {
            var srcChild = src.transform.GetChild(i);
            var dstChild = dst.transform.Find(srcChild.name);
            if (dstChild)
                CopyAllComponents(srcChild.gameObject, dstChild.gameObject);
        }
    }

    string GetRelativePath(Transform root, Transform current)
    {
        if (current == root) return "";
        return GetRelativePath(root, current.parent) + "/" + current.name;
    }

    int FindBlendShapeIndexByName(Mesh mesh, string name)
    {
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (mesh.GetBlendShapeName(i) == name)
                return i;
        }
        return -1;
    }
}
