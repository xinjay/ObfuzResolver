
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;

namespace DeobfuscateStackTrace
{

    public class SymbolMappingReader
    {
        private class MethodSignatureMapping
        {
            public string newMethodParameters;
            public string oldMethodNameWithDeclaringType;
            public string oldMethodParameters;
        }

        private class MethodSignature
        {
            public string newMethodNameWithDeclaringType;
            public List<MethodSignatureMapping> mappings = new List<MethodSignatureMapping>();
        }

        private readonly Dictionary<string, MethodSignature> _methodSignaturesMapping = new Dictionary<string, MethodSignature>();

        private readonly Dictionary<string, string> _typeNameMappings = new Dictionary<string, string>();

        public SymbolMappingReader(string mappingFile)
        {
            LoadXmlMappingFile(mappingFile);
        }

        private void LoadXmlMappingFile(string mappingFile)
        {
            var doc = new XmlDocument();
            doc.Load(mappingFile);
            var root = doc.DocumentElement;
            foreach (XmlNode node in root.ChildNodes)
            {
                if (!(node is XmlElement element))
                {
                    continue;
                }
                LoadAssemblyMapping(element);
            }
        }

        private void LoadAssemblyMapping(XmlElement ele)
        {
            if (ele.Name != "assembly")
            {
                throw new System.Exception($"Invalid node name: {ele.Name}. Expected 'assembly'.");
            }
            foreach (XmlNode node in ele.ChildNodes)
            {
                if (!(node is XmlElement element))
                {
                    continue;
                }
                if (element.Name == "type")
                {
                    LoadTypeMapping(element);
                }
            }
        }

        private void LoadTypeMapping(XmlElement ele)
        {
            if (!ele.HasAttribute("fullName"))
            {
                throw new System.Exception($"Invalid node name: {ele.Name}. attribute 'fullName' missing.");
            }
            if (!ele.HasAttribute("newFullName"))
            {
                throw new System.Exception($"Invalid node name: {ele.Name}. attribute 'newFullName' missing.");
            }
            string oldFullName = ele.Attributes["fullName"].Value;
            string newFullName = ele.Attributes["newFullName"].Value;
            _typeNameMappings[newFullName] = oldFullName;
            foreach (XmlNode node in ele.ChildNodes)
            {
                if (!(node is XmlElement c))
                {
                    continue;
                }
                if (node.Name == "method")
                {
                    LoadMethodMapping(c);
                }
            }
        }

        private (string, string) SplitMethodSignature(string signature)
        {
            int index = signature.IndexOf('(');
            if (index < 0)
            {
                return (signature, string.Empty);
            }
            string methodNameWithDeclaringType = signature.Substring(0, index);
            string methodParameters = signature.Substring(index);
            return (methodNameWithDeclaringType, methodParameters);
        }

        private void LoadMethodMapping(XmlElement ele)
        {
            if (!ele.HasAttribute("oldStackTraceSignature"))
            {
                throw new System.Exception($"Invalid node name: {ele.Name}. attribute 'oldStackTraceSignature' missing.");
            }
            if (!ele.HasAttribute("newStackTraceSignature"))
            {
                throw new System.Exception($"Invalid node name: {ele.Name}. attribute 'newStackTraceSignature' missing.");
            }
            string oldStackTraceSignature = ele.Attributes["oldStackTraceSignature"].Value;
            string newStackTraceSignature = ele.Attributes["newStackTraceSignature"].Value;


            (string oldMethodNameWithDeclaringType, string oldMethodParameters) = SplitMethodSignature(oldStackTraceSignature);
            (string newMethodNameWithDeclaringType, string newMethodParameters) = SplitMethodSignature(newStackTraceSignature);

            if (!_methodSignaturesMapping.TryGetValue(oldMethodNameWithDeclaringType, out var methodSignature))
            {
                methodSignature = new MethodSignature { newMethodNameWithDeclaringType = newMethodNameWithDeclaringType, };
                _methodSignaturesMapping[newMethodNameWithDeclaringType] = methodSignature;
            }
            methodSignature.mappings.Add(new MethodSignatureMapping
            {
                newMethodParameters = newMethodParameters,
                oldMethodNameWithDeclaringType = oldMethodNameWithDeclaringType,
                oldMethodParameters = oldMethodParameters,
            });
        }

        // xxx[T].yyy[K](a,b,c) in /path/to/file:line
        private Regex _exceptionStackTraceRegex = new Regex(@"^(\s*at\s+)([^\[\]\s]*[^.])((\[[^\[\].\s]+\])?)\.(\S[^\[\].\s]*)((\[[^\[\].\s]+\])?)\s+(\(.*\))(\s+\[\S+\]\s+in)", RegexOptions.Compiled);


        private (string, string) SplitMethodNameWithDeclaringTypeName(string name)
        {
            int lastColonIndex = name.LastIndexOf(':');
            if (lastColonIndex != -1)
            {
                string declaringTypeName = name.Substring(0, lastColonIndex);
                string methodName = name.Substring(lastColonIndex + 1);
                return (declaringTypeName, methodName);
            }
            return (string.Empty, name); // Return empty declaring type if no colon is found
        }

        private string ConvertToNormalMethodNameWithDeclaringType(string methodName)
        {
            // for .ctor or .cctor
            int lastColonIndex = methodName.LastIndexOf("..");
            if (lastColonIndex == -1)
            {
                lastColonIndex = methodName.LastIndexOf('.');
            }
            if (lastColonIndex != -1)
            {
                return methodName.Substring(0, lastColonIndex) + ":" + methodName.Substring(lastColonIndex + 1);
            }
            return methodName; // Return the original method name if no colon is found
        }

        private string ConvertToExceptionMethodNameWithDeclaringType(string methodName)
        {
            int lastColonIndex = methodName.LastIndexOf(':');
            if (lastColonIndex != -1)
            {
                return methodName.Substring(0, lastColonIndex) + "." + methodName.Substring(lastColonIndex + 1);
            }
            return methodName; // Return the original method name if no colon is found
        }

        private string ReplaceExceptionStackTraceMatch(Match m)
        {
            string obfuscatedDeclaringTypeName = m.Groups[2].Value;
            string obfuscatedMethodName = m.Groups[5].Value;
            string obfuscatedExceptionMethodNameWithDeclaringType = $"{obfuscatedDeclaringTypeName}:{obfuscatedMethodName}";
            string obfuscatedMethodParameters = m.Groups[8].Value;
            if (_methodSignaturesMapping.TryGetValue(obfuscatedExceptionMethodNameWithDeclaringType, out var methodSignature))
            {
                foreach (var mapping in methodSignature.mappings)
                {
                    if (mapping.newMethodParameters == obfuscatedMethodParameters)
                    {
                        (string oldDeclaringTypeName, string oldMethodName) = SplitMethodNameWithDeclaringTypeName(mapping.oldMethodNameWithDeclaringType);
                        return $"{m.Groups[1].Value}{oldDeclaringTypeName}{m.Groups[3].Value}.{oldMethodName}{m.Groups[6].Value}{mapping.oldMethodParameters} {m.Groups[9].Value}";
                    }
                }
                {
                    MethodSignatureMapping mapping = methodSignature.mappings[0];
                    (string oldDeclaringTypeName, string oldMethodName) = SplitMethodNameWithDeclaringTypeName(mapping.oldMethodNameWithDeclaringType);
                    return $"{m.Groups[1].Value}{oldDeclaringTypeName}{m.Groups[3].Value}.{oldMethodName}{m.Groups[6].Value}{obfuscatedMethodParameters} {m.Groups[9].Value}";
                }
            }
            return m.Value; // Return the original match if no mapping is found
        }

        public bool TryDeobfuscateExceptionStackTrace(string obfuscatedStackTraceLog, out string oldFullSignature)
        {
            oldFullSignature = _exceptionStackTraceRegex.Replace(obfuscatedStackTraceLog, ReplaceExceptionStackTraceMatch, 1);
            return oldFullSignature != obfuscatedStackTraceLog;
        }

        private Regex _normalStackTraceRegex = new Regex(@"^(\S+):(\S+)(\([^)]*\))$", RegexOptions.Compiled);

        private string ReplaceNormalStackTraceMatch(Match m)
        {
            string obfuscatedDeclaringTypeName = m.Groups[1].Value;
            string obfuscatedMethodName = m.Groups[2].Value;
            string obfuscatedMethodNameWithDeclaringType = $"{obfuscatedDeclaringTypeName}:{obfuscatedMethodName}";
            string obfuscatedMethodParameters = m.Groups[3].Value;
            if (_methodSignaturesMapping.TryGetValue(obfuscatedMethodNameWithDeclaringType, out var methodSignature))
            {
                foreach (var mapping in methodSignature.mappings)
                {
                    if (mapping.newMethodParameters == obfuscatedMethodParameters)
                    {
                        return $"{mapping.oldMethodNameWithDeclaringType}{mapping.oldMethodParameters}";
                    }
                }
                MethodSignatureMapping matchMapping = methodSignature.mappings[0];
                return $"{matchMapping.oldMethodNameWithDeclaringType}{obfuscatedMethodParameters}";
            }
            return m.Value; // Return the original match if no mapping is found
        }

        public bool TryDeobfuscateDebugLogStackTrace(string obfuscatedStackTraceLog, out string oldFullSignature)
        {
            oldFullSignature = _normalStackTraceRegex.Replace(obfuscatedStackTraceLog, ReplaceNormalStackTraceMatch, 1);
            return oldFullSignature != obfuscatedStackTraceLog;
        }

        private readonly Regex _typeNameRegex = new Regex(@"\$[$a-zA-Z_]+([./]\$[$a-zA-Z_]+)*", RegexOptions.Compiled);

        private string ReplaceTypeNameMatch(Match m)
        {
            string obfuscatedTypeName = m.Value;
            if (_typeNameMappings.TryGetValue(obfuscatedTypeName, out var originalTypeName))
            {
                return originalTypeName;
            }
            return obfuscatedTypeName; // Return the original type name if no mapping is found
        }

        public string TryDeobfuscateTypeName(string obfuscatedStackTraceLog)
        {
            return _typeNameRegex.Replace(obfuscatedStackTraceLog, ReplaceTypeNameMatch);
        }
    }
}
