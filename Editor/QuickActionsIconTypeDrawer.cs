using System.Text;
using UnityEditor;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor
{
    /// <summary>
    /// Draws every serialized <see cref="IconType"/> field (the static-shortcut list
    /// in <b>Project Settings ▸ Quick Actions</b> above all) as the usual enum popup
    /// plus one line saying what that choice does on Android: four members ship a
    /// built-in drawable, the other 25 render <b>blank</b> unless the project ships
    /// <c>ic_quickaction_&lt;name&gt;</c>. Until now that was a build-log warning,
    /// i.e. the first time a user learned it was after the build — this puts it next
    /// to the field. iOS needs nothing (system glyphs), which the note also says.
    /// </summary>
    [CustomPropertyDrawer(typeof(IconType))]
    internal sealed class QuickActionsIconTypeDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (Note(property) != null)
                height += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var field = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(field, property, label);

            var note = Note(property);
            if (note == null)
                return;
            var noteRect = new Rect(position.x, field.y + field.height + EditorGUIUtility.standardVerticalSpacing,
                position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(noteRect, note, EditorStyles.miniLabel);
        }

        private static string Note(SerializedProperty property)
        {
            // A multi-object selection with differing values has no single answer.
            if (property.hasMultipleDifferentValues)
                return null;
            return NoteFor((IconType)property.intValue);
        }

        /// <summary>
        /// The one-line Android note for <paramref name="icon"/>, or null for
        /// <see cref="IconType.None"/> (no icon requested, nothing to warn about).
        /// </summary>
        internal static string NoteFor(IconType icon)
        {
            if (icon == IconType.None)
                return null;
            if (QuickActionsBuiltInIconSet.HasAndroidArt(icon))
                return "Android: built-in drawable, ships with the package. iOS: system glyph.";
            return "Android: no built-in drawable — renders blank unless the project ships " +
                   "ic_quickaction_" + SnakeCase(icon) + ". iOS: system glyph.";
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
