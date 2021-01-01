/*
 * Modified code found URL at CodeProject
 * https://www.codeproject.com/Articles/41791/Almost-automatic-INotifyPropertyChanged-automatic
 * 
 * Original Licensed by The Microsoft Public License (Ms-PL)
 * 
 * This license governs use of the accompanying software. If you use the software, you accept this license. If you do not accept the license, do not use the software.
 *
 * 1. Definitions
 * The terms "reproduce," "reproduction," "derivative works," and "distribution" have the
 * same meaning here as under U.S. copyright law.
 * 
 * A "contribution" is the original software, or any additions or changes to the software.
 * 
 * A "contributor" is any person that distributes its contribution under this license.
 * 
 * "Licensed patents" are a contributor's patent claims that read directly on its contribution.
 * 
 * 2. Grant of Rights
 * 
 * (A) Copyright Grant- Subject to the terms of this license, including the license conditions 
 * and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free 
 * copyright license to reproduce its contribution, prepare derivative works of its contribution, and 
 * distribute its contribution or any derivative works that you create.
 * 
 * (B) Patent Grant- Subject to the terms of this license, including the license conditions and 
 * limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free 
 * license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or 
 * otherwise dispose of its contribution in the software or derivative works of the contribution 
 * in the software.
 * 
 * 3. Conditions and Limitations
 * 
 * (A) No Trademark License- This license does not grant you rights to use any contributors' 
 * name, logo, or trademarks.
 * 
 * (B) If you bring a patent claim against any contributor over patents that you claim are infringed 
 * by the software, your patent license from such contributor to the software ends automatically.
 * 
 * (C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and 
 * attribution notices that are present in the software.
 * 
 * (D) If you distribute any portion of the software in source code form, you may do so only under this 
 * license by including a complete copy of this license with your distribution. If you distribute any 
 * portion of the software in compiled or object code form, you may only do so under a license that 
 * complies with this license.
 * 
 * (E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express 
 * warranties, guarantees or conditions. You may have additional consumer rights under your local laws 
 * which this license cannot change. To the extent permitted under your local laws, the contributors exclude 
 * the implied warranties of merchantability, fitness for a particular purpose and non-infringement.
 *
 *
 *
 * History
 * 
 *  2020-11-24
 *      Majore mods to original source for my own needs and style preferences
 *      Had way more code than needed so that it can be generic, but I just 
 *      want something as lean as I can get it.  Cutting it up helps me 
 *      understand what I've got and how it works.
 * 
 */
using System;
using System.ComponentModel;
using System.Linq.Expressions;

namespace WPFChatServer
{
    public class NotifyPropertyChangeObject : INotifyPropertyChanged
    {
        // We're not tracking actual changes, just what fields got changed
        public string Changes;

        // Event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        // Initialize the change tracking
        public NotifyPropertyChangeObject()
        {
            Reset();
        }

        // Reset the object to empty (not dirty)
        public void Reset()
        {
            Changes = "/";
        }


        // Change the property if required and throw event
        public void ApplyPropertyChange<T, F>(ref F field, Expression<Func<T, object>> property, F value)
        {
            // Only do this if the value changes
            if (field == null || field.Equals(value) == false)
            {
                // Get the property
                var propertyExpression = GetMemberExpression(property);

                if (propertyExpression == null)
                    throw new InvalidOperationException("You must specify a property");

                // Actually set the value to the field that holds it
                field = value;

                // Property name
                string propertyName = propertyExpression.Member.Name;

                // Only update Changes if the propertyName is not in the string
                if (Changes.Contains("/" + propertyName + "/") == false)
                    Changes += propertyName + "/";

                // Notify the change
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // Oh, wow... it sets up the object so we can get the name
        // Need to learn more about this
        public MemberExpression GetMemberExpression<T>(Expression<Func<T, object>> expression)
        {
            // Default expression
            MemberExpression memberExpression = null;

            // Convert
            if (expression.Body.NodeType == ExpressionType.Convert)
            {
                var body = (UnaryExpression)expression.Body;
                memberExpression = body.Operand as MemberExpression;
            }
            // Member access
            else if (expression.Body.NodeType == ExpressionType.MemberAccess)
            {
                memberExpression = expression.Body as MemberExpression;
            }
            // Not a member access
            if (memberExpression == null)
                throw new ArgumentException("Not a member access", "expression");

            // Return the member expression
            return memberExpression;
        }
    }
}
