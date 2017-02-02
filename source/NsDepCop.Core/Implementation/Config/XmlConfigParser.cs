using System;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Codartis.NsDepCop.Core.Interface.Config;

namespace Codartis.NsDepCop.Core.Implementation.Config
{
    /// <summary>
    /// Parses a config provided in XML format.
    /// </summary>
    internal static class XmlConfigParser
    {
        private const string RootElementName = "NsDepCopConfig";
        private const string IsEnabledAttributeName = "IsEnabled";
        private const string CodeIssueKindAttributeName = "CodeIssueKind";
        private const string MaxIssueCountAttributeName = "MaxIssueCount";
        private const string ImplicitParentDependencyAttributeName = "ChildCanDependOnParentImplicitly";
        private const string InfoImportanceAttributeName = "InfoImportance";
        private const string ParserAttributeName = "Parser";
        private const string AllowedElementName = "Allowed";
        private const string DisallowedElementName = "Disallowed";
        private const string VisibleMembersElementName = "VisibleMembers";
        private const string TypeElementName = "Type";
        private const string OfNamespaceAttributeName = "OfNamespace";
        private const string FromAttributeName = "From";
        private const string ToAttributeName = "To";
        private const string TypeNameAttributeName = "Name";

        public static IProjectConfig ParseXmlConfig(XDocument configXml)
        {
            var configBuilder = new ProjectConfigBuilder();

            var rootElement = configXml.Element(RootElementName);
            if (rootElement == null)
                throw new Exception($"'{RootElementName}' root element not found.");

            ParseRootNodeAttributes(rootElement, configBuilder);
            ParseChildElements(rootElement, configBuilder);

            return configBuilder.ToProjectConfig();
        }

        private static void ParseRootNodeAttributes(XElement rootElement, ProjectConfigBuilder configBuilder)
        {
            configBuilder.SetIsEnabled(ParseAttribute(rootElement, IsEnabledAttributeName, bool.TryParse, ConfigDefaults.IsEnabled));
            configBuilder.SetIssueKind(ParseAttribute(rootElement, CodeIssueKindAttributeName, Enum.TryParse, ConfigDefaults.IssueKind));
            configBuilder.SetInfoImportance(ParseAttribute(rootElement, InfoImportanceAttributeName, Enum.TryParse, ConfigDefaults.InfoImportance));
            configBuilder.SetParser(ParseAttribute(rootElement, ParserAttributeName, Enum.TryParse, ConfigDefaults.Parser));

            configBuilder.SetChildCanDependOnParentImplicitly(ParseAttribute(rootElement, ImplicitParentDependencyAttributeName,
                bool.TryParse, ConfigDefaults.ChildCanDependOnParentImplicitly));
            configBuilder.SetMaxIssueCount(ParseAttribute(rootElement, MaxIssueCountAttributeName, int.TryParse, ConfigDefaults.MaxIssueReported));
        }

        private static void ParseChildElements(XElement rootElement, ProjectConfigBuilder configBuilder)
        {
            foreach (var xElement in rootElement.Elements())
            {
                switch (xElement.Name.ToString())
                {
                    case AllowedElementName:
                        ParseAllowedElement(xElement, configBuilder);
                        break;
                    case DisallowedElementName:
                        ParseDisallowedElement(xElement, configBuilder);
                        break;
                    case VisibleMembersElementName:
                        ParseVisibleMembersElement(xElement, configBuilder);
                        break;
                    default:
                        Trace.WriteLine($"Unexpected element '{xElement.Name}' ignored.");
                        break;
                }
            }
        }

        private static void ParseAllowedElement(XElement xElement, ProjectConfigBuilder configBuilder)
        {
            var allowedDependencyRule = ParseDependencyRule(xElement);

            TypeNameSet visibleTypeNames = null;

            var visibleMembersChild = xElement.Element(VisibleMembersElementName);
            if (visibleMembersChild != null)
            {
                if (allowedDependencyRule.To is NamespaceTree)
                    throw new Exception($"{GetLineInfo(xElement)}The target namespace '{allowedDependencyRule.To}' must be a single namespace.");

                if (visibleMembersChild.Attribute(OfNamespaceAttributeName) != null)
                    throw new Exception($"{GetLineInfo(xElement)}If {VisibleMembersElementName} is embedded in a dependency specification then '{OfNamespaceAttributeName}' attribute must not be defined.");

                visibleTypeNames = ParseTypeNameSet(visibleMembersChild, TypeElementName);
            }

            configBuilder.AddAllowRule(allowedDependencyRule, visibleTypeNames);
        }

        private static void ParseDisallowedElement(XElement xElement, ProjectConfigBuilder configBuilder)
        {
            var disallowedDependencyRule = ParseDependencyRule(xElement);

            configBuilder.AddDisallowRule(disallowedDependencyRule);
        }

        private static void ParseVisibleMembersElement(XElement xElement, ProjectConfigBuilder configBuilder)
        {
            var targetNamespaceName = GetAttributeValue(xElement, OfNamespaceAttributeName);
            if (targetNamespaceName == null)
                throw new Exception($"{GetLineInfo(xElement)}'{OfNamespaceAttributeName}' attribute missing.");

            var targetNamespace = TryAndReportError(xElement, () => new Namespace(targetNamespaceName.Trim()));

            var visibleTypeNames = ParseTypeNameSet(xElement, TypeElementName);
            if (!visibleTypeNames.Any())
                return;

            configBuilder.AddVisibleTypesByNamespace(targetNamespace, visibleTypeNames);
        }

        private static NamespaceDependencyRule ParseDependencyRule(XElement xElement)
        {
            var fromValue = GetAttributeValue(xElement, FromAttributeName);
            if (fromValue == null)
                throw new Exception($"{GetLineInfo(xElement)}'{FromAttributeName}' attribute missing.");

            var toValue = GetAttributeValue(xElement, ToAttributeName);
            if (toValue == null)
                throw new Exception($"{GetLineInfo(xElement)}'{ToAttributeName}' attribute missing.");

            var fromNamespaceSpecification = TryAndReportError(xElement, () => NamespaceSpecificationParser.Parse(fromValue.Trim()));
            var toNamespaceSpecification = TryAndReportError(xElement, () => NamespaceSpecificationParser.Parse(toValue.Trim()));

            return new NamespaceDependencyRule(fromNamespaceSpecification, toNamespaceSpecification);
        }

        private static T TryAndReportError<T>(XObject xObject, Func<T> parserDelegate)
        {
            try
            {
                return parserDelegate();
            }
            catch (Exception e)
            {
                throw new Exception($"{GetLineInfo(xObject)}{e.Message}", e);
            }
        }

        private static TypeNameSet ParseTypeNameSet(XElement rootElement, string elementName)
        {
            var typeNameSet = new TypeNameSet();

            foreach (var xElement in rootElement.Elements(elementName))
            {
                var typeName = GetAttributeValue(xElement, TypeNameAttributeName);
                if (typeName == null)
                    throw new Exception($"{GetLineInfo(xElement)}'{TypeNameAttributeName}' attribute missing.");

                if (!string.IsNullOrWhiteSpace(typeName))
                    typeNameSet.Add(typeName.Trim());
            }

            return typeNameSet;
        }

        /// <summary>
        /// Returns an attribute's value, or null if the attribute was not found.
        /// </summary>
        /// <param name="xElement">The parent element of the attribute.</param>
        /// <param name="attributeName">The name of the attribute.</param>
        /// <returns>The value of the attribute or null if the attribute was not found.</returns>
        private static string GetAttributeValue(XElement xElement, string attributeName)
        {
            return xElement.Attribute(attributeName)?.Value;
        }

        /// <summary>
        /// Defines the signature of a TryParse-like method, that is used to parse a value of T from string.
        /// </summary>
        /// <typeparam name="T">The type of the parse result.</typeparam>
        /// <param name="s">The string that must be parsed.</param>
        /// <param name="t">The successfully parsed value.</param>
        /// <returns>True if successfully parsed, false otherwise.</returns>
        private delegate bool TryParseMethod<T>(string s, out T t);

        /// <summary>
        /// Parses an attribute of an element to the given type. 
        /// Returns the given default value if the attribute is not found.
        /// </summary>
        /// <typeparam name="T">The type of the parse result.</typeparam>
        /// <param name="element">The element where the attribute is searched.</param>
        /// <param name="attributeName">The name of the attribute.</param>
        /// <param name="tryParseMethod">The method that should be used for parsing. Should return false on failure.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>The parsed value or the given default value if the attribute is not found.</returns>
        private static T ParseAttribute<T>(XElement element, string attributeName, TryParseMethod<T> tryParseMethod, T defaultValue)
        {
            var result = defaultValue;

            var attribute = element.Attribute(attributeName);
            if (attribute != null)
            {
                T parseResult;
                if (tryParseMethod(attribute.Value, out parseResult))
                {
                    result = parseResult;
                }
                else
                {
                    throw new FormatException($"Error parsing '{attribute.Name}' value '{attribute.Value}'.");
                }
            }

            return result;
        }

        private static string GetLineInfo(XObject xObject)
        {
            var xmlLineInfo = xObject as IXmlLineInfo;

            return xmlLineInfo.HasLineInfo()
                ? $"[Line: {xmlLineInfo.LineNumber}, Pos: {xmlLineInfo.LinePosition}] "
                : string.Empty;
        }
    }
}