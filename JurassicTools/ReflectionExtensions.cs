using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Jurassic;
using Jurassic.Library;

namespace JurassicTools
{
  public static class ReflectionExtensions
  {
    public static void CopyParametersFrom(this MethodBuilder builder, MethodInfo info, bool makeCompatible = true)
    {
      ParameterInfo[] parameters = info.GetParameters();
      JSFunctionAttribute attr = Attribute.GetCustomAttribute(info, typeof(JSFunctionAttribute)) as JSFunctionAttribute;
      if (attr != null && attr.Flags.HasFlag(JSFunctionFlags.HasEngineParameter)) parameters = parameters.Skip(1).ToArray();
      builder.SetParameters(
        parameters.Select(
          p => makeCompatible && !Attribute.IsDefined(p, typeof(ParamArrayAttribute)) ? JurassicExposer.GetConvertOrWrapType(p.ParameterType) : p.ParameterType)
                  .ToArray());
      for (int index = 0; index < parameters.Length; index++)
      {
        ParameterInfo parameterInfo = parameters[index];
        ParameterBuilder parameterBuilder = builder.DefineParameter(index + 1, parameterInfo.Attributes, parameterInfo.Name);
        if (parameterInfo.Attributes.HasFlag(ParameterAttributes.HasDefault))
        {
          parameterBuilder.SetConstant(parameterInfo.RawDefaultValue);
          parameterBuilder.CopyCustomAttributesFrom(parameterInfo, new JurassicToolsDefaultValue { Value = parameterInfo.RawDefaultValue });
        }
        else
        {
          parameterBuilder.CopyCustomAttributesFrom(parameterInfo);
        }
      }
    }

    public static void CopyCustomAttributesFrom(this MethodBuilder builder, MethodInfo info, params Attribute[] additionalAttributes)
    {
      foreach (CustomAttributeData customAttributeData in info.GetCustomAttributesData())
      {
        builder.SetCustomAttribute(customAttributeData.GetAttributeCopy());
      }
      foreach (Attribute additionalAttribute in additionalAttributes)
      {
        Type attributeType = additionalAttribute.GetType();
        PropertyInfo[] properties = attributeType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        FieldInfo[] fields = attributeType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        CustomAttributeBuilder cab = new CustomAttributeBuilder(attributeType.GetConstructors()[0], new object[0], properties,
                                                                properties.Select(p => p.GetValue(additionalAttribute, null)).ToArray(), fields,
                                                                fields.Select(f => f.GetValue(additionalAttribute)).ToArray());
        builder.SetCustomAttribute(cab);
      }
    }

    public static void CopyCustomAttributesFrom(this PropertyBuilder builder, PropertyInfo info, params Attribute[] additionalAttributes)
    {
      foreach (CustomAttributeData customAttributeData in info.GetCustomAttributesData())
      {
        builder.SetCustomAttribute(customAttributeData.GetAttributeCopy());
      }
      foreach (Attribute additionalAttribute in additionalAttributes)
      {
        Type attributeType = additionalAttribute.GetType();
        PropertyInfo[] properties = attributeType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        FieldInfo[] fields = attributeType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        CustomAttributeBuilder cab = new CustomAttributeBuilder(attributeType.GetConstructors()[0], new object[0], properties,
                                                                properties.Select(p => p.GetValue(additionalAttribute, null)).ToArray(), fields,
                                                                fields.Select(f => f.GetValue(additionalAttribute)).ToArray());
        builder.SetCustomAttribute(cab);
      }
    }

    public static void CopyCustomAttributesFrom(this ParameterBuilder builder, ParameterInfo info, params Attribute[] additionalAttributes)
    {
      foreach (CustomAttributeData customAttributeData in info.GetCustomAttributesData())
      {
        builder.SetCustomAttribute(customAttributeData.GetAttributeCopy());
      }
      foreach (Attribute additionalAttribute in additionalAttributes)
      {
        Type attributeType = additionalAttribute.GetType();
        PropertyInfo[] properties = attributeType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        FieldInfo[] fields = attributeType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        CustomAttributeBuilder cab = new CustomAttributeBuilder(attributeType.GetConstructors()[0],
                                                                new object[/*attributeType.GetConstructors()[0].GetParameters().Count()*/0], properties,
                                                                properties.Select(p => p.GetValue(additionalAttribute, null)).ToArray(), fields,
                                                                fields.Select(f => f.GetValue(additionalAttribute)).ToArray());
        builder.SetCustomAttribute(cab);
      }
    }

    public static CustomAttributeBuilder GetAttributeCopy(this CustomAttributeData attrData)
    {
      if (attrData.NamedArguments == null)
      {
        CustomAttributeBuilder attrBuilder = new CustomAttributeBuilder(attrData.Constructor, attrData.ConstructorArguments.Select(ca => ca.Value).ToArray());
        return attrBuilder;
      }
      else
      {
        CustomAttributeBuilder attrBuilder = new CustomAttributeBuilder(attrData.Constructor, attrData.ConstructorArguments.Select(ca => ca.Value).ToArray(),
                                                                        attrData.NamedArguments.Where(na => na.MemberInfo is PropertyInfo)
                                                                                .Select(na => na.MemberInfo as PropertyInfo)
                                                                                .ToArray(),
                                                                        attrData.NamedArguments.Where(na => na.MemberInfo is PropertyInfo)
                                                                                .Select(na => ((attrData.Constructor.DeclaringType == typeof(JSFunctionAttribute) && na.MemberInfo.Name == "Flags") ? (((JSFunctionFlags)na.TypedValue.Value) & ~JSFunctionFlags.HasEngineParameter) : na.TypedValue.Value))
                                                                                .ToArray(),
                                                                        attrData.NamedArguments.Where(na => na.MemberInfo is FieldInfo)
                                                                                .Select(na => na.MemberInfo as FieldInfo)
                                                                                .ToArray(),
                                                                        attrData.NamedArguments.Where(na => na.MemberInfo is FieldInfo)
                                                                                .Select(na => na.TypedValue.Value)
                                                                                .ToArray());
        return attrBuilder;
      }
    }
  }
}
