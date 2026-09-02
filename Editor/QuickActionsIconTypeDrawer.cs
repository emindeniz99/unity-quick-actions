using System.Text;
using UnityEditor;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor
{
    /// <summary>
    /// Draws every serialized <see cref="IconType"/> field (the static-shortcut list
    /// in <b>Project Settings ▸ Quick Actions</b> above all) as the usual enum popup
    /// plus a note saying what that choice does on Android: four members ship a
    /// built-in drawable, the other 25 render <b>blank</b> unless the project ships
    /// <c>ic_quickaction_&lt;name&gt;</c> — and a <b>static</b> shortcut bakes no
    /// icon at all for those unless <c>AndroidDrawable</c> names one, because the
    /// baker cannot reference a drawable that may not exist. Until now that was a
    /// build-log warning, i.e. the first time a user learned it was after the
    /// build — this puts it next to the field. iOS needs nothing (system glyphs),
    /// which the note also says.
    /// </summary>
    [CustomPropertyDrawer(typeof(IconType))]
    internal sealed class QuickActionsIconTypeDrawer : PropertyDrawer
    {
        // Inspectors can be narrow; the note must wrap rather than clip the one
        // thing it exists to show (the drawable name), so its height is measured.
        private static GUIStyle _noteStyle;

        private static GUIStyle NoteStyle() =>
            _noteStyle ?? (_noteStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true });

        // The width GetPropertyHeight does not receive: the inspector's, less the
        // margins the property rect loses to it. Slightly narrow is the safe error —
        // a line too many, never a clipped one.
        private static float NoteWidth(float propertyWidth) =>
            propertyWidth > 0 ? propertyWidth : EditorGUIUtility.currentViewWidth - 40f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight;
            var note = Note(property);
            if (note != null)
                height += EditorGUIUtility.standardVerticalSpacing +
                          NoteStyle().CalcHeight(new GUIContent(note), NoteWidth(0f));
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var field = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(field, property, label);

            var note = Note(property);
            if (note == null)
                return;
            var content = new GUIContent(note);
            var noteRect = new Rect(position.x, field.y + field.height + EditorGUIUtility.standardVerticalSpacing,
                position.width, NoteStyle().CalcHeight(content, NoteWidth(position.width)));
            EditorGUI.LabelField(noteRect, content, NoteStyle());
        }

        private static string Note(SerializedProperty property)
        {
            // A multi-object selection with differing values has no single answer.
            if (property.hasMultipleDifferentValues)
                return null;
            // The settings asset's list is the STATIC set: those items are baked at
            // build time, where "the project ships the drawable" is not enough.
            var isStatic = property.serializedObject != null &&
                           property.serializedObject.targetObject is QuickActionsSettings;
            return NoteFor((IconType)property.intValue, BuiltInIconsEnabled(), isStatic);
        }

        // The "Write built-in Android icons" toggle lives on the settings asset, and
        // finding that asset is a project-wide AssetDatabase scan. The asset is cached
        // once found (reading the toggle off it is free); only while NONE is cached
        // does the drawer look again, and then at most once a second — a repaint loop
        // never becomes a scan, and GetOrNull's duplicate-assets warning (logged per
        // call when a project holds two) can fire once, not once per second. No asset
        // means the default: on. Unity's fake-null drops a deleted asset out of the cache.
        private static QuickActionsSettings _settings;
        private static double _nextSettingsScan;

        private static bool BuiltInIconsEnabled()
        {
            if (_settings == null)
            {
                var now = EditorApplication.timeSinceStartup;
                if (now < _nextSettingsScan)
                    return true;
                _nextSettingsScan = now + 1.0;
                _settings = QuickActionsSettings.GetOrNull();
                if (_settings == null)
                    return true;
            }
            return _settings.WriteBuiltInAndroidIcons;
        }

        /// <summary>
        /// The Android note for <paramref name="icon"/>, or null for
        /// <see cref="IconType.None"/> (no icon requested, nothing to warn about).
        /// <paramref name="builtInIconsEnabled"/> is the settings asset's "Write
        /// built-in Android icons" toggle: off, the four built-ins are not written
        /// either. <paramref name="isStatic"/> says the field belongs to a static
        /// (baked) shortcut, where a drawable the project merely ships is never
        /// picked up — the baker bakes only a built-in or whatever
        /// <c>AndroidDrawable</c> names, and warns otherwise.
        /// </summary>
        internal static string NoteFor(IconType icon, bool builtInIconsEnabled, bool isStatic)
        {
            if (icon == IconType.None)
                return null;
            var projectDrawable = "ic_quickaction_" + SnakeCase(icon);
            const string ios = " iOS: system glyph.";
            if (QuickActionsBuiltInIconSet.HasAndroidArt(icon))
            {
                if (builtInIconsEnabled)
                    return (isStatic
                        ? "Android: built-in drawable, baked into this static shortcut."
                        : "Android: built-in drawable, ships with the package.") + ios;
                return "Android: built-in drawable, but \"Write built-in Android icons\" is off — " +
                       (isStatic
                           ? "this static shortcut bakes no icon unless AndroidDrawable names a drawable you ship (e.g. " + projectDrawable + ")."
                           : "renders blank unless the project ships " + projectDrawable + ".") + ios;
            }
            return "Android: no built-in drawable — " +
                   (isStatic
                       ? "a static shortcut bakes no icon for this choice; ship a drawable and name it in AndroidDrawable (e.g. " + projectDrawable + ")."
                       : "renders blank unless the project ships " + projectDrawable + ".") + ios;
        }

        /// <summary>
        /// The <c>&lt;name&gt;</c> Android looks up for a catalog member: the member
        /// lower-cased with an underscore before each later capital
        /// (<c>MarkLocation</c> → <c>mark_location</c>) — the rule the Java
        /// <c>ICON_NAMES</c> table follows, which a harness test pins member by member.
        /// </summary>
        internal static string SnakeCase(IconType icon)
        {
            var member = icon.ToString();
            var sb = new StringBuilder(member.Length + 4);
            for (var i = 0; i < member.Length; i++)
            {
                var c = member[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                        sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
