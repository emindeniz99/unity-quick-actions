// Harness-only tests (same reason as AndroidStaticLocalizationTests): the Android
// post-processor lives behind a UNITY_ANDROID-constrained asmdef the Unity Test
// Runner cannot reference on another build target.
//
// What they pin is EscapeResValue, which runs over every static shortcut's label
// on every build that has one. A string-resource VALUE needs Android's own
// escaping on top of XML's: after aapt2 parses the XML, a bare apostrophe or
// double quote is a span delimiter, a leading '@' or '?' is a resource
// reference, and unquoted edge whitespace is trimmed. Each of those is an aapt2
// hard failure or a silently wrong on-device label in a real Gradle build, and
// nothing before Gradle sees it — so the escaper had no coverage at all until
// this file: a regression in any one branch was invisible to the whole suite.
//
// Driven through AppendLocalized rather than EscapeResValue directly: that is the
// real emission path (it is the caller), and it keeps the escaper private.
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using EminDeniz99.QuickActions;
using EminDeniz99.QuickActions.Editor;

namespace EminDeniz99.QuickActions.Tests
{
    [TestFixture]
    public class AndroidStaticEscapingTests
    {
        // The escaped form of one label, as it is actually written into the
        // <string> element: everything between the element's '>' and its '</'.
        private static string Escaped(string text)
        {
            var buckets = new SortedDictionary<string, StringBuilder>(System.StringComparer.OrdinalIgnoreCase);
            var emitted = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            QuickActionsBuildPostProcessorAndroid.AppendLocalized(buckets, emitted,
                new List<LocalizedText> { new LocalizedText("fr", text) }, "qa_short_0", "play");
            Assert.AreEqual(1, buckets.Count, "the fixture must emit exactly one locale bucket");
            var body = buckets["fr"].ToString();
            const string open = "formatted=\"false\">";
            var start = body.IndexOf(open, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "no <string> element was written: " + body);
            start += open.Length;
            var end = body.IndexOf("</string>", start, System.StringComparison.Ordinal);
            Assert.Greater(end, start, "the <string> element was not closed: " + body);
            return body.Substring(start, end - start);
        }

        [Test]
        public void XmlMetacharacters_AreEntityEncoded_SoTheFileStaysWellFormed()
        {
            // An unencoded '&' or '<' does not mangle one label, it makes the whole
            // generated file unparseable and fails the build.
            Assert.AreEqual("Tom &amp; Jerry", Escaped("Tom & Jerry"));
            Assert.AreEqual("&lt;b&gt;bold&lt;/b&gt;", Escaped("<b>bold</b>"));
            Assert.AreEqual("a &amp;amp; b", Escaped("a &amp; b"),
                "an ampersand already inside an entity must be encoded again, not passed through");
        }

        [Test]
        public void AndroidSpanDelimiters_AreBackslashEscaped()
        {
            // These survive XML parsing and are then read by the Android resource
            // layer, where a bare one truncates or breaks the label.
            Assert.AreEqual("Mom\\'s List", Escaped("Mom's List"));
            Assert.AreEqual("Say \\\"hi\\\"", Escaped("Say \"hi\""));
            Assert.AreEqual("back\\\\slash", Escaped("back\\slash"));
            // A backslash the author wrote must not turn the next character into an
            // escape of its own — it is doubled first.
            Assert.AreEqual("\\\\n", Escaped("\\n"));
        }

        [Test]
        public void LeadingResourceReferenceMarkers_AreEscaped()
        {
            // '@Home' or '?Help' read as a resource reference and fail the build with
            // an unresolved-symbol error pointing at a file nobody wrote by hand.
            Assert.AreEqual("\\@Home", Escaped("@Home"));
            Assert.AreEqual("\\?Help", Escaped("?Help"));
            // Only the FIRST character carries that meaning.
            Assert.AreEqual("me@home", Escaped("me@home"));
            Assert.AreEqual("what?", Escaped("what?"));
        }

        [Test]
        public void EdgeWhitespace_IsPreservedByQuotingTheWholeValue()
        {
            // aapt trims unquoted leading/trailing whitespace, so a deliberate
            // alignment space would vanish between the settings asset and the device.
            Assert.AreEqual("\" pad \"", Escaped(" pad "));
            Assert.AreEqual("\"trailing \"", Escaped("trailing "));
            Assert.AreEqual("\" leading\"", Escaped(" leading"));
            // Interior whitespace never needed quoting and must not get it.
            Assert.AreEqual("two words", Escaped("two words"));
        }

        [Test]
        public void QuotingAndEscapingCompose_InBothOrders()
        {
            // The two rules can apply to one value, and the quote wrapper goes on
            // LAST so it wraps the already-escaped text rather than being escaped
            // by it.
            Assert.AreEqual("\"\\@Home \"", Escaped("@Home "),
                "a leading marker is escaped and the trailing space still quoted");
            Assert.AreEqual("\" @Home\"", Escaped(" @Home"),
                "a leading space means '@' is no longer the value's first character, " +
                "and the quote wrapper keeps it that way for aapt");
            Assert.AreEqual("\"Mom\\'s \"", Escaped("Mom's "));
        }

        [Test]
        public void ControlCharacters_AreDropped_RatherThanWrittenIntoTheXml()
        {
            // XML 1.0 cannot represent most C0 controls at all, escaped or not, so a
            // label pasted from somewhere else must not be able to poison the file.
            Assert.AreEqual("ab", Escaped("ab"));
            Assert.AreEqual("line1line2", Escaped("line1\nline2"));
            Assert.AreEqual("tabbed", Escaped("tab\tbed"));
        }

        [Test]
        public void APlainLabel_IsWrittenThrough_Unchanged()
        {
            // The escaper must not touch what needs nothing — including '%', which
            // formatted="false" on the element already neutralises.
            Assert.AreEqual("Daily Reward", Escaped("Daily Reward"));
            Assert.AreEqual("50% off", Escaped("50% off"));
            Assert.AreEqual("Jouer à nouveau", Escaped("Jouer à nouveau"));
            Assert.AreEqual("播放", Escaped("播放"));
        }
    }
}
