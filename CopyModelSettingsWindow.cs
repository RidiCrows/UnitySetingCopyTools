using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;

public class CopyModelSettingsWindow : EditorWindow
{
    GameObject sourcePrefab;
    GameObject targetPrefab;

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
        EditorGUILayout.HelpBox("可拖入场景中的模型对象，或直接拖入 Prefab 文件（会自动打开并保存修改）", MessageType.Info);
        EditorGUILayout.Space(8);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("源模型 (Source)", EditorStyles.boldLabel);
        sourcePrefab = (GameObject)EditorGUILayout.ObjectField(sourcePrefab, typeof(GameObject), true);

        EditorGUILayout.LabelField("目标模型 (Target)", EditorStyles.boldLabel);
        targetPrefab = (GameObject)EditorGUILayout.ObjectField(targetPrefab, typeof(GameObject), true);

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
            CopySettingsSmart();
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

    // ----------- ����ʶ��ִ�и��� -----------
    void CopySettingsSmart()
    {
        if (sourcePrefab == null || targetPrefab == null)
        {
            EditorUtility.DisplayDialog("完成", "模型设置已成功复制！", "确定");
            return;
        }

        bool srcIsPrefabAsset = IsPrefabAsset(sourcePrefab);
        bool dstIsPrefabAsset = IsPrefabAsset(targetPrefab);

        GameObject srcRoot = null;
        GameObject dstRoot = null;
        string dstPath = null;

        try
        {
            if (srcIsPrefabAsset)
            {
                string path = AssetDatabase.GetAssetPath(sourcePrefab);
                srcRoot = PrefabUtility.LoadPrefabContents(path);
                Debug.Log($"🔹已加载源Prefab{path}");
            }
            else
            {
                srcRoot = sourcePrefab;
            }

            if (dstIsPrefabAsset)
            {
                dstPath = AssetDatabase.GetAssetPath(targetPrefab);
                dstRoot = PrefabUtility.LoadPrefabContents(dstPath);
                Debug.Log($"🔹已加载目标Prefab：{dstPath}");
            }
            else
            {
                dstRoot = targetPrefab;
            }

            // traverse source hierarchy and copy to matching target objects
            int copiedCount = CopyHierarchy(srcRoot, dstRoot);
            Debug.Log($"? �ܹ����Ƶ� {copiedCount} ������\\n");

            if (dstIsPrefabAsset && dstRoot != null)
            {
                PrefabUtility.SaveAsPrefabAsset(dstRoot, dstPath);
                PrefabUtility.UnloadPrefabContents(dstRoot);
                Debug.Log("?? �ѱ����޸ĵ�Ŀ��Prefab��");
                AssetDatabase.SaveAssets();
            }
            else if (dstRoot != null)
            {
                EditorUtility.SetDirty(dstRoot);
                if (dstRoot.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(dstRoot.scene);
            }

            if (srcIsPrefabAsset && srcRoot != null)
                PrefabUtility.UnloadPrefabContents(srcRoot);

            EditorUtility.DisplayDialog("���", "ģ�������ѳɹ����ƣ�", "ȷ��");
        }
        catch (Exception ex)
        {
            Debug.LogError($"? ����ʧ��: {ex.Message}\\n{ex.StackTrace}");
        }
    }

    bool IsPrefabAsset(GameObject obj)
    {
        return obj != null && !obj.scene.IsValid() &&
               PrefabUtility.GetPrefabAssetType(obj) != PrefabAssetType.NotAPrefab;
    }

    // traverse source hierarchy, find matching target by relative path or name and copy settings
    int CopyHierarchy(GameObject srcRoot, GameObject dstRoot)
    {
        if (srcRoot == null || dstRoot == null) return 0;
        int count = 0;
        var allSrc = srcRoot.GetComponentsInChildren<Transform>(true);
        foreach (var t in allSrc)
        {
            string rel = GetRelativePath(srcRoot.transform, t);
            GameObject dstObj = null;
            if (string.IsNullOrEmpty(rel))
            {
                dstObj = dstRoot; // root
            }
            else
            {
                dstObj = dstRoot.transform.Find(rel)?.gameObject;
                if (dstObj == null)
                {
                    // try find by name anywhere under dstRoot
                    var candidates = dstRoot.GetComponentsInChildren<Transform>(true)
                        .Where(x => x.name == t.name).ToArray();
                    dstObj = candidates.Length > 0 ? candidates[0].gameObject : null;
                }
            }

            if (dstObj != null)
            {
                bool did = CopyToMatchingObject(t.gameObject, dstObj);
                if (did) count++;
            }
        }
        return count;
    }

    bool CopyToMatchingObject(GameObject srcObj, GameObject dstObj)
    {
        if (srcObj == null || dstObj == null) return false;
        bool any = false;

        // Transform
        if (copyTransform)
        {
            dstObj.transform.localPosition = srcObj.transform.localPosition;
            dstObj.transform.localRotation = srcObj.transform.localRotation;
            dstObj.transform.localScale = srcObj.transform.localScale;
            any = true;
        }

        // Animator
        if (copyAnimator)
        {
            var srcAnim = srcObj.GetComponent<Animator>();
            if (srcAnim)
            {
                var dstAnim = dstObj.GetComponent<Animator>();
                if (!dstAnim) dstAnim = dstObj.AddComponent<Animator>();
                dstAnim.runtimeAnimatorController = srcAnim.runtimeAnimatorController;
                dstAnim.avatar = srcAnim.avatar;
                EditorUtility.SetDirty(dstAnim);
                any = true;
            }
        }

        // SkinnedMeshRenderer: materials + blendshapes
        if (copyMaterials || copyBlendShapes)
        {
            var srcS = srcObj.GetComponent<SkinnedMeshRenderer>();
            var dstS = dstObj.GetComponent<SkinnedMeshRenderer>();
            if (srcS && dstS)
            {
                if (copyMaterials)
                {
                    dstS.sharedMaterials = srcS.sharedMaterials;
                    EditorUtility.SetDirty(dstS);
                    any = true;
                }

                if (copyBlendShapes && srcS.sharedMesh != null && dstS.sharedMesh != null)
                {
                    for (int j = 0; j < srcS.sharedMesh.blendShapeCount; j++)
                    {
                        string name = srcS.sharedMesh.GetBlendShapeName(j);
                        int idx = FindBlendShapeIndexByName(dstS.sharedMesh, name);
                        if (idx >= 0)
                        {
                            float w = srcS.GetBlendShapeWeight(j);
                            dstS.SetBlendShapeWeight(idx, w);
                            any = true;
                        }
                    }
                    EditorUtility.SetDirty(dstS);
                }
            }
        }

        // DynamicBone / PhysBone
        if (copyDynamicBones)
        {
            var srcBones = srcObj.GetComponents<Component>().Where(c => c && (c.GetType().Name.Contains("DynamicBone") || c.GetType().Name.Contains("PhysBone"))).ToArray();
            foreach (var sb in srcBones)
            {
                var dstComp = dstObj.GetComponent(sb.GetType());
                if (!dstComp) dstComp = dstObj.AddComponent(sb.GetType());
                EditorUtility.CopySerialized(sb, dstComp);
                EditorUtility.SetDirty(dstComp);
                any = true;
            }
        }

        // VRC Avatar Descriptor
#if VRC_SDK_VRCSDK3
        if (copyVRCDescriptor)
        {
            var srcDesc = srcObj.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (srcDesc)
            {
                var dstDesc = dstObj.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                if (!dstDesc) dstDesc = dstObj.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                EditorUtility.CopySerialized(srcDesc, dstDesc);
                EditorUtility.SetDirty(dstDesc);
                any = true;
            }
        }
#endif

        // Copy all other components
        if (copyAllComponents)
        {
            var srcComps = srcObj.GetComponents<Component>();
            foreach (var srcComp in srcComps)
            {
                if (!srcComp || srcComp is Transform) continue;
                var dstComp = dstObj.GetComponent(srcComp.GetType());
                if (!dstComp) dstComp = dstObj.AddComponent(srcComp.GetType());
                EditorUtility.CopySerialized(srcComp, dstComp);
                EditorUtility.SetDirty(dstComp);
                any = true;
            }
        }

        if (any)
        {
            EditorUtility.SetDirty(dstObj);
        }

        return any;
    }

    int FindBlendShapeIndexByName(Mesh mesh, string name)
    {
        if (mesh == null) return -1;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (mesh.GetBlendShapeName(i) == name)
                return i;
        }
        return -1;
    }

    string GetRelativePath(Transform root, Transform current)
    {
        if (root == null || current == null) return null;
        if (root == current) return "";
        var parts = new List<string>();
        var t = current;
        while (t != null && t != root)
        {
            parts.Add(t.name);
            t = t.parent;
        }
        if (t != root) // root ���� ancestor
            return null;
        parts.Reverse();
        return string.Join("/", parts);
    }
}