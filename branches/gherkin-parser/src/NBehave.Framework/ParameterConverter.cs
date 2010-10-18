using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NBehave.Narrator.Framework
{
    public class ParameterConverter
    {
        private readonly ActionCatalog _actionCatalog;

        public ParameterConverter(ActionCatalog actionCatalog)
        {
            _actionCatalog = actionCatalog;
        }

        public object[] GetParametersForActionStepText(ActionStepText actionStepText)
        {
            var action = _actionCatalog.GetAction(actionStepText);
            var paramNames = GetParameterNames(action);
            var match = action.ActionStepMatcher.Match(actionStepText.Step);
            Func<int, string> getValues = i => match.Groups[paramNames[i]].Value;

            return GetParametersForActionStepText(action, paramNames, getValues);
        }

        public object[] GetParametersForActionStepText(ActionStepText actionStepText, Row row)
        {
            var action = _actionCatalog.GetAction(actionStepText);
            var paramNames = action.ParameterInfo.Select(a => a.Name).ToList();
            Func<int, string> getValues = i => row.ColumnValues[paramNames[i].ToLower()];

            return GetParametersForActionStepText(action, paramNames, getValues);
        }

        private object[] GetParametersForActionStepText(ActionMethodInfo action, ICollection<string> paramNames, Func<int, string> getValue)
        {
            var args = action.ParameterInfo;
            var values = new object[args.Length];

            for (var argNumber = 0; argNumber < paramNames.Count; argNumber++)
            {
                var strParam = getValue(argNumber);
                values[argNumber] = ChangeParameterType(strParam, args[argNumber]);
            }

            return values;
        }

        private List<string> GetParameterNames(ActionMethodInfo actionValue)
        {
            return actionValue.GetParameterNames();
        }
      
        private object ChangeParameterType(string strParam, ParameterInfo paramType)
        {
            if (paramType.ParameterType.IsArray)
                return CreateArray(strParam, paramType.ParameterType);
            if (IsGenericIEnumerable(paramType))
                return CreateList(strParam, paramType.ParameterType);
            return Convert.ChangeType(strParam, paramType.ParameterType);
        }

        private object CreateArray(string strParam, Type arrayOfType)
        {
            var strParamAsArray = GetParamAsArray(strParam);
            var typedArray = Activator.CreateInstance(arrayOfType, strParamAsArray.Length);
            var typeInList = arrayOfType.GetElementType();
            SetValues(strParamAsArray, typeInList, typedArray, "SetValue");
            return typedArray;
        }

        private object CreateList(string param, Type parameterType)
        {
            var innerType = parameterType.GetGenericArguments()[0];
            var genericList = CreateGeneric(typeof (List<>), innerType);
            var strParamAsArray = GetParamAsArray(param);
            SetValues(strParamAsArray, innerType, genericList, "AddValue");
            return genericList;
        }

        private bool IsGenericIEnumerable(ParameterInfo paramType)
        {
            if (paramType.ParameterType.IsGenericType == false)
                return false;

            var genericArgs = paramType.ParameterType.GetGenericArguments();
            if (genericArgs.Length > 1)
                throw new NotSupportedException("Sorry, nbehave only supports one generic parameter");
            var ien = CreateGeneric(typeof(List<>), genericArgs[0]);
            return paramType.ParameterType.IsAssignableFrom(ien.GetType());
        }

        public object CreateGeneric(Type generic, Type innerType)
        {
            var specificType = generic.MakeGenericType(new[] { innerType });
            return Activator.CreateInstance(specificType, null);
        }

        private void SetValues(string[] strParamAsArray, Type typeInList, object typedArray, string function)
        {
            var method = GetType().GetMethod(function, BindingFlags.NonPublic | BindingFlags.Instance);
            var types = new[] { typeInList };
            var genMethod = method.MakeGenericMethod(types);
            for (var i = 0; i < strParamAsArray.Length; i++)
            {
                var value = Convert.ChangeType(strParamAsArray[i], typeInList);
                genMethod.Invoke(this, new[] { typedArray, i, value });
            }
        }

        private string[] GetParamAsArray(string strParam)
        {
            var strParamAsArray = strParam.Replace(Environment.NewLine, "\n").Split(new[] { ',' });
            TrimValues(strParamAsArray);
            var trimmedArray = TrimEnd(strParamAsArray);
            return trimmedArray;
        }

        private void TrimValues(string[] strParamAsArray)
        {
            for (var i = 0; i < strParamAsArray.Length; i++)
            {
                if (string.IsNullOrEmpty(strParamAsArray[i]) == false)
                    strParamAsArray[i] = strParamAsArray[i].Trim();
            }
        }

        private string[] TrimEnd(string[] strParamAsArray)
        {
            while (string.IsNullOrEmpty(strParamAsArray.Last()))
                strParamAsArray = strParamAsArray.Take(strParamAsArray.Length - 1).ToArray();
            return strParamAsArray;
        }

        // ReSharper disable UnusedMember.Local
        // ReSharper disable UnusedParameter.Local

        //This method is called with reflection by the CreateArray method
        private void SetValue<T>(T[] array, int index, T value)
        {
            array[index] = value;
        }

        //This method is called with reflection by the CreateArray method
        private void AddValue<T>(ICollection<T> array, int index, T value)
        {
            array.Add(value);
        }

        // ReSharper restore UnusedParameter.Local
        // ReSharper restore UnusedMember.Local
    }
}