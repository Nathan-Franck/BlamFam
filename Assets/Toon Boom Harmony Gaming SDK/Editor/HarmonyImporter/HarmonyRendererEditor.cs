using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ToonBoom.Harmony
{
    [CustomEditor(typeof(HarmonyRenderer), true)]
    [CanEditMultipleObjects]
    public class HarmonyRendererEditor : Editor
    {
        private ReorderableList _groupSkinList;

        private SerializedProperty _serializedProject;
        private SerializedProperty _serializedCurrentClipIndex;
        private SerializedProperty _serializedGroupSkins;

        protected void OnEnable()
        {
            _serializedProject = serializedObject.FindProperty(nameof(HarmonyRenderer.Project));
            _serializedCurrentClipIndex = serializedObject.FindProperty(nameof(HarmonyRenderer.CurrentClipIndex));
            _serializedGroupSkins = serializedObject.FindProperty(nameof(HarmonyRenderer.GroupSkins));

            if (_groupSkinList == null)
            {
                var renderer = (HarmonyRenderer)target;
                _groupSkinList = new ReorderableList(renderer.GroupSkins, typeof(GroupSkin), true, true, true, true);
                _groupSkinList.onAddCallback = AddGroupSkin;
                _groupSkinList.onCanAddCallback = CanAddGroupSkin;
                _groupSkinList.drawElementCallback = DrawGroupSkin;
                _groupSkinList.drawHeaderCallback = DrawGroupSkinListHeader;
                _groupSkinList.onChangedCallback = OnGroupSkinListChanged;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            SerializedProperty iterator = serializedObject.GetIterator();
            iterator.NextVisible(true);
            do
            {
                if (iterator.propertyPath == nameof(HarmonyRenderer.CurrentFrame))
                {
                    DrawFrame(iterator, _serializedCurrentClipIndex, _serializedProject);
                }
                else if (iterator.propertyPath == nameof(HarmonyRenderer.CurrentClipIndex))
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Clip Settings", EditorStyles.boldLabel);
                    DrawClip(iterator, _serializedProject);
                }
                else if (iterator.propertyPath == nameof(HarmonyRenderer.SpriteSheetIndex))
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Render Settings", EditorStyles.boldLabel);
                    DrawSpriteSheet(iterator, _serializedProject);
                }
                else if (iterator.propertyPath == nameof(HarmonyRenderer.GroupSkins))
                {
                    if (_serializedProject.objectReferenceValue != null)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Skin Settings", EditorStyles.boldLabel);

                        DrawSkin(iterator);
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledScope(iterator.propertyPath == "m_Script"))
                        EditorGUILayout.PropertyField(iterator, true);

                    if (iterator.propertyPath == nameof(HarmonyRenderer.AnimationSettings))
                    {

                        if (GUILayout.Button("Update Animator"))
                        {
                            foreach (var renderer in targets.OfType<HarmonyRenderer>())
                            {
                                renderer.AnimationSettings.UpdateAnimationAssets(renderer, false);
                            }
                        }
                        else if (GUILayout.Button("Update Current Clip"))
                        {
                            foreach (var renderer in targets.OfType<HarmonyRenderer>())
                            {
                                renderer.AnimationSettings.UpdateAnimationAssets(renderer, true);
                            }
                        }
                    }
                }
            }
            while (iterator.NextVisible(false));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSkin(SerializedProperty skinProperty)
        {
            Rect position = EditorGUILayout.GetControlRect(false, _groupSkinList.GetHeight());
            GUIContent label = new GUIContent(skinProperty.displayName, skinProperty.tooltip);
            using (new EditorGUI.PropertyScope(position, label, skinProperty))
            {
                _groupSkinList.DoList(position);
            }
        }

        private void DrawGroupSkinListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Starting Skins");
        }

        private void DrawGroupSkin(Rect rect, int index, bool isActive, bool isFocused)
        {
            HarmonyProject project = (HarmonyProject)_serializedProject.objectReferenceValue;

            var groupSkinList = (IList<GroupSkin>)_groupSkinList.list;
            GroupSkin groupSkin = groupSkinList[index];
            var element = _serializedGroupSkins.FindPropertyRelative("_item" + index);

            var groupId = element.FindPropertyRelative(nameof(GroupSkin.GroupId));
            var skinId = element.FindPropertyRelative(nameof(GroupSkin.SkinId));

            EditorGUIUtility.labelWidth = 40.0f;

            Rect position;
            GUIContent label;

            position = new Rect(rect.x, rect.y, rect.width / 2, rect.height);
            label = new GUIContent("Group", groupId.tooltip);
            using (new EditorGUI.PropertyScope(position, label, groupId))
            {
                groupSkin.GroupId = groupId.intValue = EditorGUI.Popup(EditorGUI.PrefixLabel(position, label), groupId.intValue, project.Groups.ToArray());
            }

            position.x += position.width;
            label = new GUIContent("Skin", skinId.tooltip);
            using (new EditorGUI.PropertyScope(position, label, skinId))
            {
                Dictionary<int, HashSet<int>> groupToSkinIds = new Dictionary<int, HashSet<int>>() {
                    { 0, new HashSet<int>() { 0 } }
                };
                foreach (var node in project.Nodes)
                {
                    if (!groupToSkinIds.TryGetValue(node.GroupId, out HashSet<int> skinIds))
                    {
                        skinIds = new HashSet<int>();
                        groupToSkinIds.Add(node.GroupId, skinIds);
                    }
                    foreach (var skinIdx in node.SkinIds)
                    {
                        groupToSkinIds[0].Add(skinIdx);
                        skinIds.Add(skinIdx);
                    }
                }
                var skinsForGroup = groupToSkinIds[groupId.intValue]
                    .Select((skinIndex, popupIndex) => new { skinIndex, popupIndex, name = skinIndex == 0 ? "none" : project.Skins[skinIndex] })
                    .ToList();
                var popupResult = EditorGUI.Popup(
                    EditorGUI.PrefixLabel(position, label),
                    skinsForGroup.Find(skin => skin.skinIndex == skinId.intValue)?.popupIndex ?? 0,
                    skinsForGroup.Select(skin => skin.name).ToArray());
                groupSkin.SkinId = skinId.intValue = skinsForGroup
                    .Find(skin => skin.popupIndex == popupResult)
                    .skinIndex;
            }

            groupSkinList[index] = groupSkin;
            EditorGUIUtility.labelWidth = 0.0f;
        }

        private bool CanAddGroupSkin(ReorderableList list)
        {
            return list.list.Count < GroupSkinList.MAX_COUNT;
        }

        private void AddGroupSkin(ReorderableList list)
        {
            list.list.Add(new GroupSkin());
        }

        private struct SpriteSheetName
        {
            public string ResolutionName;
            public string PaletteName;
            public SpriteSheetName(string spriteSheetName)
            {
                var splits = spriteSheetName.Split('-');
                ResolutionName = splits[0];
                PaletteName = string.Join("-", splits.Skip(1));
            }
        }

        private void DrawSpriteSheet(SerializedProperty property, SerializedProperty projectProperty)
        {
            HarmonyProject project = (HarmonyProject)projectProperty.objectReferenceValue;
            if (!project)
                return;

            var spriteSheetNames = project.SpriteSheets.Select(clip => new SpriteSheetName(clip.ResolutionName));
            var resolutionNames = spriteSheetNames.Select(name => name.ResolutionName).Distinct().ToList();
            var paletteNames = spriteSheetNames.Select(name => name.PaletteName).Distinct().ToList();
            var currentName = new SpriteSheetName(project.SpriteSheets[property.intValue].ResolutionName);
            var currentResolutionIndex = resolutionNames.IndexOf(currentName.ResolutionName);
            var currentPaletteIndex = paletteNames.IndexOf(currentName.PaletteName);

            int resolutionIndex;
            Rect position = EditorGUILayout.GetControlRect();
            GUIContent label = new GUIContent("Resolution", property.tooltip);
            using (new EditorGUI.PropertyScope(position, label, property))
            {
                resolutionIndex = EditorGUI.Popup(
                    EditorGUI.PrefixLabel(position, label),
                    currentResolutionIndex,
                    resolutionNames.ToArray());
            }

            int paletteIndex;
            position = EditorGUILayout.GetControlRect();
            label = new GUIContent("Palette", property.tooltip);
            using (new EditorGUI.PropertyScope(position, label, property))
            {
                paletteIndex = EditorGUI.Popup(
                    EditorGUI.PrefixLabel(position, label),
                    currentPaletteIndex,
                    paletteNames.ToArray());
            }

            property.intValue = project.GetSheetIndex(resolutionNames[resolutionIndex], paletteNames[paletteIndex]);
        }

        private void DrawFrame(SerializedProperty frameProperty, SerializedProperty clipIndexProperty, SerializedProperty projectProperty)
        {
            HarmonyProject project = (HarmonyProject)projectProperty.objectReferenceValue;
            if (!project)
                return;

            float clipFrameCount = project.GetClipByIndex(clipIndexProperty.intValue).FrameCount;

            Rect position = EditorGUILayout.GetControlRect();
            GUIContent label = new GUIContent(frameProperty.displayName, frameProperty.tooltip);
            using (new EditorGUI.PropertyScope(position, label, frameProperty))
            {
                frameProperty.floatValue = EditorGUI.Slider(EditorGUI.PrefixLabel(position, label), frameProperty.floatValue, 1.0f, clipFrameCount);
            }
        }

        private void DrawClip(SerializedProperty clipIndexProperty, SerializedProperty projectProperty)
        {
            HarmonyProject project = (HarmonyProject)projectProperty.objectReferenceValue;
            if (!project)
                return;

            Rect position = EditorGUILayout.GetControlRect();
            GUIContent label = new GUIContent(clipIndexProperty.displayName, clipIndexProperty.tooltip);
            using (new EditorGUI.PropertyScope(position, label, clipIndexProperty))
            {
                clipIndexProperty.intValue = EditorGUI.IntSlider(EditorGUI.PrefixLabel(position, label), clipIndexProperty.intValue, 0, project.Clips.Count - 1);
            }

            position = EditorGUILayout.GetControlRect();
            label = new GUIContent("Current Clip", clipIndexProperty.tooltip);
            using (new EditorGUI.PropertyScope(position, label, clipIndexProperty))
            {
                clipIndexProperty.intValue = EditorGUI.Popup(EditorGUI.PrefixLabel(position, label), clipIndexProperty.intValue, project.Clips.Select(clip => clip.DisplayName).ToArray());
            }
        }

        private void OnGroupSkinListChanged(ReorderableList list)
        {
            _serializedGroupSkins.FindPropertyRelative("_count").intValue = list.list.Count;

            var groupSkinList = (IList<GroupSkin>)list.list;
            for (int i = 0; i < GroupSkinList.MAX_COUNT; ++i)
            {
                var itemProperty = _serializedGroupSkins.FindPropertyRelative("_item" + i);
                if (i < groupSkinList.Count)
                {
                    itemProperty.FindPropertyRelative(nameof(GroupSkin.GroupId)).intValue = groupSkinList[i].GroupId;
                    itemProperty.FindPropertyRelative(nameof(GroupSkin.SkinId)).intValue = groupSkinList[i].SkinId;
                }
                else
                {
                    itemProperty.FindPropertyRelative(nameof(GroupSkin.GroupId)).intValue = 0;
                    itemProperty.FindPropertyRelative(nameof(GroupSkin.SkinId)).intValue = 0;
                }
            }
        }
    }
}