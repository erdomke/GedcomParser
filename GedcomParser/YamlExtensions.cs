using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace GedcomParser
{
  internal static class YamlExtensions
  {
    public static YamlNode Item(this YamlNode node, string key)
    {
      if (node is YamlMappingNode mapping)
      {
        if (mapping.Children.TryGetValue(key, out var child))
          return child;
        else
          return null;
      }
      else if (node == null)
        return null;
      throw new InvalidOperationException("The node is not a mapping node.");
    }

    public static IEnumerable<KeyValuePair<YamlNode, YamlNode>> EnumerateObject(this YamlNode node)
    {
      if (node is YamlMappingNode mapping)
        return mapping.Children;
      else if (node == null)
        return Enumerable.Empty<KeyValuePair<YamlNode, YamlNode>>();
      throw new InvalidOperationException("The node is not a mapping node.");
    }

    public static YamlNode Item(this YamlNode node, int index)
    {
      if (node is YamlSequenceNode sequence)
        return sequence.Children[index];
      else if (node == null)
        return null;
      throw new InvalidOperationException("The node is not a sequence node.");
    }

    public static IEnumerable<YamlNode> EnumerateArray(this YamlNode node)
    {
      if (node is YamlSequenceNode sequence)
        return sequence.Children;
      else if (node == null)
        return Enumerable.Empty<YamlNode>();
      throw new InvalidOperationException("The node is not a sequence node.");
    }

    public static string String(this YamlNode node)
    {
      if (node is YamlScalarNode scalar)
        return scalar.Value;
      else if (node == null)
        return null;
      throw new InvalidOperationException("The node is not a scalar node.");
    }
  }
}
