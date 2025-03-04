/**
 * Copyright (c) 2014-present, Facebook, Inc. All rights reserved.
 *
 * You are hereby granted a non-exclusive, worldwide, royalty-free license to use,
 * copy, modify, and distribute this software in source code or binary form for use
 * in connection with the web services and APIs provided by Facebook.
 *
 * As with any software that integrates with the Facebook platform, your use of
 * this software is subject to the Facebook Developer Principles and Policies
 * [http://developers.facebook.com/policy/]. This copyright notice shall be
 * included in all copies or substantial portions of the software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
 * FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
 * COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
 * IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
 * CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

namespace Facebook.Unity
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    internal static class Utilities
    {
        private const string WarningMissingParameter = "Did not find expected value '{0}' in dictionary";
        private static Dictionary<string, string> commandLineArguments;

        public delegate void Callback<T>(T obj);

        public static Dictionary<string, string> CommandLineArguments
        {
            get
            {
                if (commandLineArguments != null)
                {
                    return commandLineArguments;
                }

                var localCommandLineArguments = new Dictionary<string, string>();
                var arguments = Environment.GetCommandLineArgs();
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (arguments[i].StartsWith("/") || arguments[i].StartsWith("-"))
                    {
                        var value = i + 1 < arguments.Length ? arguments[i + 1] : null;
                        localCommandLineArguments.Add(arguments[i], value);
                    }
                }

                commandLineArguments = localCommandLineArguments;
                return commandLineArguments;
            }
        }

        public static bool TryGetValue<T>(
            this IDictionary<string, object> dictionary,
            string key,
            out T value)
        {
            object resultObj;
            if (dictionary.TryGetValue(key, out resultObj) && resultObj is T)
            {
                value = (T)resultObj;
                return true;
            }

            value = default(T);
            return false;
        }

        public static long TotalSeconds(this DateTime dateTime)
        {
            TimeSpan t = dateTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            long secondsSinceEpoch = (long)t.TotalSeconds;
            return secondsSinceEpoch;
        }

        public static T GetValueOrDefault<T>(
            this IDictionary<string, object> dictionary,
            string key,
            bool logWarning = true)
        {
            T result;
            if (!dictionary.TryGetValue<T>(key, out result) && logWarning)
            {
                FacebookLogger.Warn(WarningMissingParameter, key);
            }

            return result;
        }

        public static string ToCommaSeparateList(this IEnumerable<string> list)
        {
            if (list == null)
            {
                return string.Empty;
            }

            return string.Join(",", list.ToArray());
        }

        public static string AbsoluteUrlOrEmptyString(this Uri uri)
        {
            if (uri == null)
            {
                return string.Empty;
            }

            return uri.AbsoluteUri;
        }

        public static string GetUserAgent(string productName, string productVersion)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}",
                productName,
                productVersion);
        }

        public static string ToJson(this IDictionary<string, object> dictionary)
        {
            return MiniJSON.Json.Serialize(dictionary);
        }

        public static void AddAllKVPFrom<T1, T2>(this IDictionary<T1, T2> dest, IDictionary<T1, T2> source)
        {
            foreach (T1 key in source.Keys)
            {
                dest[key] = source[key];
            }
        }

        public static AccessToken ParseAccessTokenFromResult(IDictionary<string, object> resultDictionary)
        {
            string userID = resultDictionary.GetValueOrDefault<string>(LoginResult.UserIdKey);
            string accessToken = resultDictionary.GetValueOrDefault<string>(LoginResult.AccessTokenKey);
            DateTime expiration = Utilities.ParseExpirationDateFromResult(resultDictionary);
            ICollection<string> permissions = Utilities.ParsePermissionFromResult(resultDictionary);
            DateTime? lastRefresh = Utilities.ParseLastRefreshFromResult(resultDictionary);
            string graphDomain = resultDictionary.GetValueOrDefault<string>(LoginResult.GraphDomain);

            return new AccessToken(
                accessToken,
                userID,
                expiration,
                permissions,
                lastRefresh,
                graphDomain);
        }

        public static AuthenticationToken ParseAuthenticationTokenFromResult(IDictionary<string, object> resultDictionary)
        {
            string tokenString = resultDictionary.GetValueOrDefault<string>(LoginResult.AuthTokenString);
            string nonce = resultDictionary.GetValueOrDefault<string>(LoginResult.AuthNonce);

            return new AuthenticationToken(tokenString, nonce);
        }

        public static string ToStringNullOk(this object obj)
        {
            if (obj == null)
            {
                return "null";
            }

            return obj.ToString();
        }

        // Use this instead of reflection to avoid crashing at
        // runtime due to Unity's stripping
        public static string FormatToString(
            string baseString,
            string className,
            IDictionary<string, string> propertiesAndValues)
        {
            StringBuilder sb = new StringBuilder();
            if (baseString != null)
            {
                sb.Append(baseString);
            }

            sb.AppendFormat("\n{0}:", className);
            foreach (var kvp in propertiesAndValues)
            {
                string value = kvp.Value != null ? kvp.Value : "null";
                sb.AppendFormat("\n\t{0}: {1}", kvp.Key, value);
            }

            return sb.ToString();
        }

        private static DateTime ParseExpirationDateFromResult(IDictionary<string, object> resultDictionary)
        {
            DateTime expiration;
            if (Constants.IsWeb)
            {
                // For canvas we get back the time as seconds since now instead of in epoch time.
                long timeTillExpiration = resultDictionary.GetValueOrDefault<long>(LoginResult.ExpirationTimestampKey);
                expiration = DateTime.UtcNow.AddSeconds(timeTillExpiration);
            }
            else
            {
                string expirationStr = resultDictionary.GetValueOrDefault<string>(LoginResult.ExpirationTimestampKey);
                int expiredTimeSeconds;
                if (int.TryParse(expirationStr, out expiredTimeSeconds) && expiredTimeSeconds > 0)
                {
                    if (Constants.IsGameroom)
                    {
                        expiration = DateTime.UtcNow.AddSeconds(expiredTimeSeconds);
                    }
                    else
                    {
                        expiration = Utilities.FromTimestamp(expiredTimeSeconds);
                    }
                }
                else
                {
                    expiration = DateTime.MaxValue;
                }
            }

            return expiration;
        }

        private static DateTime? ParseLastRefreshFromResult(IDictionary<string, object> resultDictionary)
        {
            string lastRefreshStr = resultDictionary.GetValueOrDefault<string>(LoginResult.LastRefreshKey, false);
            int lastRefresh;
            if (int.TryParse(lastRefreshStr, out lastRefresh) && lastRefresh > 0)
            {
                return Utilities.FromTimestamp(lastRefresh);
            }
            else
            {
                return null;
            }
        }

        private static ICollection<string> ParsePermissionFromResult(IDictionary<string, object> resultDictionary)
        {
            string permissions;
            IEnumerable<object> permissionList;

            // For permissions we can get the result back in either a comma separated string or
            // a list depending on the platform.
            if (resultDictionary.TryGetValue(LoginResult.PermissionsKey, out permissions))
            {
                permissionList = permissions.Split(',');
            }
            else if (!resultDictionary.TryGetValue(LoginResult.PermissionsKey, out permissionList))
            {
                permissionList = new string[0];
                FacebookLogger.Warn("Failed to find parameter '{0}' in login result", LoginResult.PermissionsKey);
            }

            return permissionList.Select(permission => permission.ToString()).ToList();
        }

        public static IList<Product> ParseCatalogFromResult(IDictionary<string, object> resultDictionary)
        {
            object catalogObject;
            IList<Product> products = new List<Product>();

            if (resultDictionary.TryGetValue("success", out catalogObject))
            {
                IList<object> deserializedCatalogObject = (IList<object>) MiniJSON.Json.Deserialize(catalogObject as string);
                foreach (IDictionary<string, object> product in deserializedCatalogObject) {
                    string title = product["title"].ToStringNullOk();
                    string productID = product["productID"].ToStringNullOk();
                    string description = product["description"].ToStringNullOk();
                    string imageURI = product.ContainsKey("imageURI") ? product["imageURI"].ToStringNullOk() : "";
                    string price = product["price"].ToStringNullOk();
                    double? priceAmount = product.ContainsKey("priceAmount") ? (double?) product["priceAmount"] : null;
                    string priceCurrencyCode = product["priceCurrencyCode"].ToStringNullOk();

                    products.Add(new Product(title, productID, description, imageURI, price, priceAmount, priceCurrencyCode));
                }
                return products;
            }
            else
            {
                return null;
            }
        }

        public static IList<Purchase> ParsePurchasesFromResult(IDictionary<string, object> resultDictionary)
        {
            object purchasesObject;
            IList<Purchase> purchases = new List<Purchase>();

            if (resultDictionary.TryGetValue("success", out purchasesObject))
            {
                IList<object> deserializedPurchasesObject = (IList<object>) MiniJSON.Json.Deserialize(purchasesObject as string);
                foreach (IDictionary<string, object> purchase in (List<object>) deserializedPurchasesObject) {
                    purchases.Add(ParsePurchaseFromDictionary(purchase));
                }

                return purchases;
            }
            else
            {
                return null;
            }
        }

        public static Purchase ParsePurchaseFromResult(IDictionary<string, object> resultDictionary)
        {
            object purchaseObject;
            if (resultDictionary.TryGetValue("success", out purchaseObject))
            {
                IDictionary<string, object> deserializedPurchaseObject = (IDictionary<string, object>) MiniJSON.Json.Deserialize(purchaseObject as string);
                return ParsePurchaseFromDictionary(deserializedPurchaseObject);
            }
            else
            {
                return null;
            }
        }

        private static Purchase ParsePurchaseFromDictionary(IDictionary<string, object> purchase) {
            bool isConsumed = (bool)purchase["isConsumed"];
            string developerPayload = purchase.ContainsKey("developerPayload") ? purchase["developerPayload"].ToStringNullOk() : "";
            string paymentActionType = purchase["paymentActionType"].ToStringNullOk();
            string paymentID = purchase["paymentID"].ToStringNullOk();
            string productID = purchase["productID"].ToStringNullOk();
            IDictionary<string, object> purchasePrice = (IDictionary<string, object>) purchase["purchasePrice"];
            long purchaseTime = (long)purchase["purchaseTime"];
            string purchaseToken = purchase["purchaseToken"].ToStringNullOk();
            string signedRequest = purchase["signedRequest"].ToStringNullOk();

            return new Purchase(developerPayload, isConsumed, paymentActionType, paymentID, productID, purchasePrice, purchaseTime, purchaseToken, signedRequest);
        }

        public static IDictionary<string, string> ParseStringDictionaryFromString(string input) {
            Dictionary<string, object> dict = MiniJSON.Json.Deserialize(input) as Dictionary<string, object>;
            if (dict == null || dict.Count == 0) {
                return null;
            }
            IDictionary<string, string> result = new Dictionary<string, string>();
            foreach (KeyValuePair<string, object> kvp in dict)
            {
                result.Add(kvp.Key, kvp.Value != null ? kvp.Value.ToString() : "");
            }
            return result;
        }

        // key parameter is the key whose value is the inner dictionary that will be parsed
        public static IDictionary<string, string> ParseInnerStringDictionary(IDictionary<string, object> resultDictionary, string key)
        {
            object resultObject;
            if (resultDictionary.TryGetValue(key, out resultObject))
            {
                return ParseStringDictionaryFromString(resultObject as string);
            }
            else
            {
                return null;
            }
        }

        public static DateTime FromTimestamp(int timestamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(timestamp);
        }
    }
}
