Imports Microsoft.VisualBasic
Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Data
Imports System.Drawing
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms
Imports DevExpress.XtraEditors.Repository
Imports DevExpress.XtraGrid.Views.Base
Imports DevExpress.XtraGrid.Views.Grid
Imports DevExpress.XtraGrid.Columns
Imports System.Collections
Imports System.Globalization

Namespace GridControlTypeConverter
	''' <summary>
	''' Defines a type converter for enum values that converts enum values to 
	''' and from string representations using resources
	''' </summary>
	''' <remarks>
	''' This class makes localization of display values for enums in a project easy.  Simply
	''' derive a class from this class and pass the ResourceManagerin the constructor.  
	''' 
	''' <code lang="C#" escaped="true" >
	''' class LocalizedEnumConverter : ResourceEnumConverter
	''' {
	'''    public LocalizedEnumConverter(Type type)
	'''        : base(type, Properties.Resources.ResourceManager)
	'''    {
	'''    }
	''' }    
	''' </code>
	''' 
	''' <code lang="Visual Basic" escaped="true" >
	''' Public Class LocalizedEnumConverter
	''' 
	'''    Inherits ResourceEnumConverter
	'''    Public Sub New(ByVal sType as Type)
	'''        MyBase.New(sType, My.Resources.ResourceManager)
	'''    End Sub
	''' End Class    
	''' </code>
	''' 
	''' Then define the enum values in the resource editor.   The names of
	''' the resources are simply the enum value prefixed by the enum type name with an
	''' underscore separator eg MyEnum_MyValue.  You can then use the TypeConverter attribute
	''' to make the LocalizedEnumConverter the default TypeConverter for the enums in your
	''' project.
	''' </remarks>
	Public Class ResourceEnumConverter
		Inherits System.ComponentModel.EnumConverter
		Private Class LookupTable
			Inherits Dictionary(Of String, Object)
		End Class
		Private _lookupTables As New Dictionary(Of CultureInfo, LookupTable)()
		Private _resourceManager As System.Resources.ResourceManager
		Private _isFlagEnum As Boolean = False
		Private _flagValues As Array

		''' <summary>
		''' Get the lookup table for the given culture (creating if necessary)
		''' </summary>
		''' <param name="culture"></param>
		''' <returns></returns>
		Private Function GetLookupTable(ByVal culture As CultureInfo) As LookupTable
			Dim result As LookupTable = Nothing
			If culture Is Nothing Then
				culture = CultureInfo.CurrentCulture
			End If

			If (Not _lookupTables.TryGetValue(culture, result)) Then
				result = New LookupTable()
				For Each value As Object In GetStandardValues()
					Dim text As String = GetValueText(culture, value)
					If text IsNot Nothing Then
						result.Add(value.ToString(),text)
					End If
				Next value
				_lookupTables.Add(culture, result)
			End If
			Return result
		End Function

		''' <summary>
		''' Return the text to display for a simple value in the given culture
		''' </summary>
		''' <param name="culture">The culture to get the text for</param>
		''' <param name="value">The enum value to get the text for</param>
		''' <returns>The localized text</returns>
		Public Function GetValueText(ByVal culture As CultureInfo, ByVal value As Object) As String
			Dim type As Type = value.GetType()
			Dim resourceName As String = String.Format("{0}_{1}", type.Name, value.ToString())
			Dim result As String = _resourceManager.GetString(resourceName, culture)
			If result Is Nothing Then
				result = resourceName
			End If
			Return result
		End Function

		''' <summary>
		''' Return true if the given value is can be represented using a single bit
		''' </summary>
		''' <param name="value"></param>
		''' <returns></returns>
		Private Function IsSingleBitValue(ByVal value As ULong) As Boolean
			Select Case value
				Case 0
					Return False
				Case 1
					Return True
			End Select
			Return ((value And (value - 1)) = 0)
		End Function

		''' <summary>
		''' Return the text to display for a flag value in the given culture
		''' </summary>
		''' <param name="culture">The culture to get the text for</param>
		''' <param name="value">The flag enum value to get the text for</param>
		''' <returns>The localized text</returns>
		Private Function GetFlagValueText(ByVal culture As CultureInfo, ByVal value As Object) As String
			' if there is a standard value then use it
			'
			If System.Enum.IsDefined(value.GetType(), value) Then
				Return GetValueText(culture, value)
			End If

			' otherwise find the combination of flag bit values
			' that makes up the value
			'
			Dim lValue As ULong = Convert.ToUInt32(value)
			Dim result As String = Nothing
			For Each flagValue As Object In _flagValues
				Dim lFlagValue As ULong = Convert.ToUInt32(flagValue)
				If IsSingleBitValue(lFlagValue) Then
					If (lFlagValue And lValue) = lFlagValue Then
						Dim valueText As String = GetValueText(culture, flagValue)
						If result Is Nothing Then
							result = valueText
						Else
							result = String.Format("{0}, {1}", result, valueText)
						End If
					End If
				End If
			Next flagValue
			Return result
		End Function

		''' <summary>
		''' Return the Enum value for a simple (non-flagged enum)
		''' </summary>
		''' <param name="culture">The culture to convert using</param>
		''' <param name="text">The text to convert</param>
		''' <returns>The enum value</returns>
		Private Function GetValue(ByVal culture As CultureInfo, ByVal text As String) As Object
			Dim lookupTable As LookupTable = GetLookupTable(culture)
			Dim result As Object = Nothing
			lookupTable.TryGetValue(text, result)
			Return result
		End Function

		''' <summary>
		''' Return the Enum value for a flagged enum
		''' </summary>
		''' <param name="culture">The culture to convert using</param>
		''' <param name="text">The text to convert</param>
		''' <returns>The enum value</returns>
		Private Function GetFlagValue(ByVal culture As CultureInfo, ByVal text As String) As Object
			Dim lookupTable As LookupTable = GetLookupTable(culture)
			Dim textValues() As String = text.Split(","c)
			Dim result As ULong = 0
			For Each textValue As String In textValues
				Dim value As Object = Nothing
				Dim trimmedTextValue As String = textValue.Trim()
				If (Not lookupTable.TryGetValue(trimmedTextValue, value)) Then
					Return Nothing
				End If
				result = result Or Convert.ToUInt32(value)
			Next textValue
			Return System.Enum.ToObject(EnumType, result)
		End Function

		''' <summary>
		''' Create a new instance of the converter using translations from the given resource manager
		''' </summary>
		''' <param name="type"></param>
		''' <param name="resourceManager"></param>
		Public Sub New(ByVal type As Type, ByVal resourceManager As System.Resources.ResourceManager)
			MyBase.New(type)
			_resourceManager = resourceManager
			Dim flagAttributes() As Object = type.GetCustomAttributes(GetType(FlagsAttribute), True)
			_isFlagEnum = flagAttributes.Length > 0
			If _isFlagEnum Then
				_flagValues = System.Enum.GetValues(type)
			End If
		End Sub

		''' <summary>
		''' Convert string values to enum values
		''' </summary>
		''' <param name="context"></param>
		''' <param name="culture"></param>
		''' <param name="value"></param>
		''' <returns></returns>
		Public Overrides Overloads Function ConvertFrom(ByVal context As System.ComponentModel.ITypeDescriptorContext, ByVal culture As System.Globalization.CultureInfo, ByVal value As Object) As Object
			If TypeOf value Is String Then
				Dim result As Object = If((_isFlagEnum), GetFlagValue(culture, CStr(value)), GetValue(culture, CStr(value)))
				If result Is Nothing Then
					result = MyBase.ConvertFrom(context, culture, value)
				End If
				Return result
			Else
				Return MyBase.ConvertFrom(context, culture, value)
			End If
		End Function
	   ' override 
		''' <summary>
		''' Convert the enum value to a string
		''' </summary>
		''' <param name="context"></param>
		''' <param name="culture"></param>
		''' <param name="value"></param>
		''' <param name="destinationType"></param>
		''' <returns></returns>
		Public Overrides Overloads Function ConvertTo(ByVal context As System.ComponentModel.ITypeDescriptorContext, ByVal culture As System.Globalization.CultureInfo, ByVal value As Object, ByVal destinationType As Type) As Object
			If value IsNot Nothing AndAlso destinationType Is GetType(String) Then
				Dim result As Object = If((_isFlagEnum), GetFlagValueText(culture, value), GetValueText(culture, value))
				Return result
			Else
				Return MyBase.ConvertTo(context, culture, value, destinationType)
			End If
		End Function

		''' <summary>
		''' Convert the given enum value to string using the registered type converter
		''' </summary>
		''' <param name="value">The enum value to convert to string</param>
		''' <returns>The localized string value for the enum</returns>
		Public Shared Overloads Function ConvertToString(ByVal value As System.Enum) As String
			Dim converter As TypeConverter = TypeDescriptor.GetConverter(value.GetType())
			Return converter.ConvertToString(value)
		End Function

		''' <summary>
		''' Return a list of the enum values and their associated display text for the given enum type
		''' </summary>
		''' <param name="enumType">The enum type to get the values for</param>
		''' <param name="culture">The culture to get the text for</param>
		''' <returns>
		''' A list of KeyValuePairs where the key is the enum value and the value is the text to display
		''' </returns>
		''' <remarks>
		''' This method can be used to provide localized binding to enums in ASP.NET applications.   Unlike 
		''' windows forms the standard ASP.NET controls do not use TypeConverters to convert from enum values
		''' to the displayed text.   You can bind an ASP.NET control to the list returned by this method by setting
		''' the DataValueField to "Key" and theDataTextField to "Value". 
		''' </remarks>
		Public Shared Function GetValues(ByVal enumType As Type, ByVal culture As CultureInfo) As List(Of KeyValuePair(Of System.Enum, String))
			Dim result As New List(Of KeyValuePair(Of System.Enum, String))()
			Dim converter As TypeConverter = TypeDescriptor.GetConverter(enumType)
			For Each value As System.Enum In System.Enum.GetValues(enumType)
				Dim pair As New KeyValuePair(Of System.Enum, String)(value, converter.ConvertToString(Nothing, culture, value))
				result.Add(pair)
			Next value
			Return result
		End Function

		''' <summary>
		''' Return a list of the enum values and their associated display text for the given enum type in the current UI Culture
		''' </summary>
		''' <param name="enumType">The enum type to get the values for</param>
		''' <returns>
		''' A list of KeyValuePairs where the key is the enum value and the value is the text to display
		''' </returns>
		''' <remarks>
		''' This method can be used to provide localized binding to enums in ASP.NET applications.   Unlike 
		''' windows forms the standard ASP.NET controls do not use TypeConverters to convert from enum values
		''' to the displayed text.   You can bind an ASP.NET control to the list returned by this method by setting
		''' the DataValueField to "Key" and theDataTextField to "Value". 
		''' </remarks>
		Public Shared Function GetValues(ByVal enumType As Type) As List(Of KeyValuePair(Of System.Enum, String))
			Return GetValues(enumType, CultureInfo.CurrentUICulture)
		End Function
	End Class
End Namespace
