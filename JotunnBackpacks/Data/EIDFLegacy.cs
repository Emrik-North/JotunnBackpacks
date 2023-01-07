/* EIDFLegacy.cs
 * 
 * Converted by Vapok to no longer rely on Extended Item Data Framework.
 * Now utilized Iron Gate's m_customData object to store backpack data.
 *
 * This file is specifically crafted to deal with Backpack Conversion from Extended Item Data Framework
 * 
 * In a future version, it is at the discretion of the Mod Author as to whether pull this class out to no loner be backwards compatible.
 */


using System;

namespace JotunnBackpacks.Data
{
    public static class EIDFLegacy
    {
        public const string StartDelimiter = "<|";
        public const string EndDelimiter = "|>";
        public const string StartDelimiterEscaped = "randyknapp.mods.extendeditemdataframework.startdelimiter";
        public const string EndDelimiterEscaped = "randyknapp.mods.extendeditemdataframework.enddelimiter";

        public const string CrafterNameType = "c";
        public const string UniqueIdType = "u";

        public static string RestoreDataText(string text)
        {
            return string.IsNullOrEmpty(text) ? text : text.Replace(StartDelimiterEscaped, StartDelimiter).Replace(EndDelimiterEscaped, EndDelimiter);
        }

        public static bool IsLegacyEIDFItem(this ItemDrop.ItemData itemData)
        {
            if (itemData == null || string.IsNullOrEmpty(itemData.m_crafterName))
                return false;

            return ContainsEncodedCrafterName(itemData.m_crafterName);
        }

        public static bool ContainsEncodedCrafterName(string value)
        {
            return value.Contains($"{StartDelimiter}{CrafterNameType}{EndDelimiter}");
        }

        public static bool IsCustomItem(this ItemDrop.ItemData itemData)
        {
            if (itemData == null || string.IsNullOrEmpty(itemData.m_crafterName))
                return false;

            return itemData.m_crafterName.Contains($"{StartDelimiter}{BackpackComponent.TypeID}{EndDelimiter}");
        }

        public static string FormatCrafterName(string tooltipResult)
        {
            if (string.IsNullOrEmpty(tooltipResult) || !ContainsEncodedCrafterName(tooltipResult))
                return tooltipResult;

            //Check for EIDF Usage
            if (tooltipResult.Contains($"{StartDelimiter}{CrafterNameType}{EndDelimiter}"))
            {
                var eidfCrafterNameStartIndex = tooltipResult.IndexOf($"{StartDelimiter}{CrafterNameType}{EndDelimiter}");
                var eidfCrafterNameStopIndex = tooltipResult.IndexOf("</color>", eidfCrafterNameStartIndex);
                var length = eidfCrafterNameStopIndex - eidfCrafterNameStartIndex;
                var encodedCrafterName = tooltipResult.Substring(eidfCrafterNameStartIndex, length);
                var formatedCrafterName = GetEIDFTypeData(encodedCrafterName, CrafterNameType);
                tooltipResult = tooltipResult.Replace(encodedCrafterName, formatedCrafterName);
            }

            return tooltipResult;
        }

        public static string GetCustomItemFromCrafterName(this ItemDrop.ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.m_crafterName))
                return null;

            return GetEIDFTypeData(item.m_crafterName, typeof(BackpackComponent).AssemblyQualifiedName);
        }

        public static string GetCrafterName(this ItemDrop.ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.m_crafterName))
                return null;

            if (!item.IsLegacyEIDFItem())
                return item.m_crafterName;

            return GetEIDFTypeData(item.m_crafterName, CrafterNameType) ?? string.Empty;
        }
        public static string GetCrafterName(string encodedCrafterName)
        {
            if (string.IsNullOrEmpty(encodedCrafterName) || !ContainsEncodedCrafterName(encodedCrafterName))
                return encodedCrafterName;

            return GetEIDFTypeData(encodedCrafterName, CrafterNameType) ?? string.Empty;
        }

        public static string GetUniqueId(this ItemDrop.ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.m_crafterName))
                return null;

            if (!item.IsLegacyEIDFItem())
                return item.m_crafterName;

            return GetEIDFTypeData(item.m_crafterName, UniqueIdType) ?? string.Empty;
        }

        private static string GetEIDFTypeData(string encodedCrafterName, string typeID)
        {
            if (string.IsNullOrEmpty(encodedCrafterName) || string.IsNullOrEmpty(typeID))
                return null;

            var serializedComponents = encodedCrafterName.Split(new[] { StartDelimiter }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var component in serializedComponents)
            {
                var parts = component.Split(new[] { EndDelimiter }, StringSplitOptions.None);
                var typeString = RestoreDataText(parts[0]);

                if (typeString.Equals(typeID))
                {
                    var data = parts.Length == 2 ? parts[1] : string.Empty;
                    if (string.IsNullOrEmpty(data))
                        continue;

                    return RestoreDataText(data);
                }
            }

            return null;
        }
    }
}
