using System.Collections.Generic;
using UnityEngine;

public class ConditionManager : MonoBehaviour
{
    [SerializeField] private bool defaultConditionValue = false;

    private Dictionary<string, object> conditionDatabase = new Dictionary<string, object>();

    public void SetCondition(string key, object value)
    {
        if (conditionDatabase.ContainsKey(key))
        {
            conditionDatabase[key] = value;
        }
        else
        {
            conditionDatabase.Add(key, value);
        }
    }

    public object GetConditionValue(string key)
    {
        if (conditionDatabase.ContainsKey(key))
        {
            return conditionDatabase[key];
        }
        return null;
    }

    public bool CheckCondition(string conditionKey, string expectedValue)
    {
        if (!conditionDatabase.ContainsKey(conditionKey))
        {
            Debug.LogWarning($"未找到条件键: {conditionKey}，使用默认值: {defaultConditionValue}");
            return defaultConditionValue;
        }

        object currentValue = conditionDatabase[conditionKey];

        if (currentValue is bool boolValue)
        {
            if (bool.TryParse(expectedValue, out bool expectedBool))
            {
                return boolValue == expectedBool;
            }
        }
        else if (currentValue is int intValue)
        {
            if (int.TryParse(expectedValue, out int expectedInt))
            {
                return intValue >= expectedInt;
            }
        }
        else if (currentValue is float floatValue)
        {
            if (float.TryParse(expectedValue, out float expectedFloat))
            {
                return floatValue >= expectedFloat;
            }
        }
        else if (currentValue is string stringValue)
        {
            return stringValue == expectedValue;
        }

        return defaultConditionValue;
    }
}