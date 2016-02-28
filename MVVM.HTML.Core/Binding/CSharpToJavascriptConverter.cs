﻿using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using MVVM.Component;
using MVVM.HTML.Core.Binding.Extension;
using MVVM.HTML.Core.Binding.GlueObject;
using MVVM.HTML.Core.JavascriptEngine.JavascriptObject;

namespace MVVM.HTML.Core.Binding
{
    internal class CSharpToJavascriptConverter 
    {
        private readonly IJavascriptSessionCache _Cacher;
        private readonly IJSCommandFactory _CommandFactory;
        private readonly HTMLViewContext _Context;

        public CSharpToJavascriptConverter(HTMLViewContext context, IJavascriptSessionCache icacher, IJSCommandFactory commandFactory)
        {
            _CommandFactory = commandFactory;
            _Context = context;
            _Cacher = icacher;
        }

        internal IJSCSGlue Map(object ifrom, object iadditional = null)
        {
            return _Context.WebView.Evaluate(() => InternalMap(ifrom, iadditional));
        }

        private IJSCSGlue InternalMap(object from, object iadditional=null)
        {
            if (from == null)
                return JSGenericObject.CreateNull(_Context);

            var res = _Cacher.GetCached(from);
            if (res != null)
                return res;

            var command = from as ICommand;
            if (command != null)
                return _CommandFactory.Build(command);

            var simpleCommand = from as ISimpleCommand;
            if (simpleCommand != null)
                return _CommandFactory.Build(simpleCommand);

            var resultCommand = from as IResultCommand;
            if (resultCommand != null)
                return _CommandFactory.Build(resultCommand);

            IJavascriptObject value;
            if (_Context.WebView.Factory.CreateBasic(from, out value))
                return new JSBasicObject(value, from);

            if (from.GetType().IsEnum)
            {
                var trueres = new JSBasicObject(_Context.WebView.Factory.CreateEnum((Enum)from), from);
                _Cacher.CacheLocal(from, trueres);
                return trueres;
            }

            var ienfro = from as IEnumerable;
            if (ienfro!=null)
                return  Convert(ienfro);

            var resobject = _Context.WebView.Factory.CreateObject(true);

            var gres = new JSGenericObject(_Context, resobject, from);
            _Cacher.Cache(from, gres);

            MappNested(from, resobject,gres);
            MappNested(iadditional, resobject, gres);

            return gres;
        }

        private void MappNested(object from, IJavascriptObject resobject, JSGenericObject gres)
        {
            if (from == null)
                return;

           var propertyInfos = from.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead);

            foreach (var propertyInfo in propertyInfos)
            {
                var propertyName = propertyInfo.Name;
                object childvalue;
                try
                {
                    childvalue = propertyInfo.GetValue(from, null); 
                }
                catch(Exception e)
                {
                    Trace.WriteLine(string.Format("MVVM for HTML: Unable to convert property {0} from {1} exception {2}", propertyName, from, e));
                    continue;
                }

                var childres = InternalMap(childvalue);          
                _Context.WebView.Run(() => resobject.SetValue(propertyName, childres.JSValue));
                gres.UpdateCSharpProperty(propertyName, childres);
            }
        }

        private IJSCSGlue Convert(IEnumerable source)
        {
            var res = new JSArray(_Context, source.Cast<object>().Select(s => Map(s)), source);
            _Cacher.Cache(source, res);
            return res;
        }
    }
}
