/*******************************************************************************
 * Copyright (C) 2019-2025 MarbleBag
 * Copyright (C) 2026 Shuro
 *
 * This program is free software: you can redistribute it and/or modify it under
 * the terms of the GNU Affero General Public License as published by the Free
 * Software Foundation, version 3.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 *******************************************************************************/

using System;
using System.Globalization;

namespace Gobchat.Core.Runtime
{
    internal sealed class DIContext : IDIContext
    {
        private readonly TinyIoC.TinyIoCContainer _container = new TinyIoC.TinyIoCContainer();

        public DIContext()
        {
        }

        public void Dispose()
        {
            _container.Dispose();
        }

        public void Unregister<RegisterType>(string name) where RegisterType : class
        {
            _container.Unregister<RegisterType>(name);
        }

        public void Unregister<RegisterType>() where RegisterType : class
        {
            _container.Unregister<RegisterType>();
        }

        public void Register<RegisterType>(Func<IDIContext, object?, RegisterType> factory) where RegisterType : class
        {
            _container.Register<RegisterType>((c, p) => factory(this, null));
        }

        public void Register<RegisterType>(Func<IDIContext, object?, RegisterType> factory, string name) where RegisterType : class
        {
            _container.Register<RegisterType>((c, p) => factory(this, null), name);
        }

        public void Register<RegisterType>(RegisterType instance) where RegisterType : class
        {
            _container.Register<RegisterType>(instance);
        }

        public void Register<RegisterType>(string name) where RegisterType : class
        {
            _container.Register<RegisterType>(name);
        }

        public void Register<RegisterType>(RegisterType instance, string name) where RegisterType : class
        {
            _container.Register<RegisterType>(instance, name);
        }

        public void Register<RegisterType>() where RegisterType : class
        {
            _container.Register<RegisterType>();
        }

        public RegisterType Resolve<RegisterType>() where RegisterType : class
        {
            try
            {
                return _container.Resolve<RegisterType>();
            }
            catch (TinyIoC.TinyIoCResolutionException e)
            {
                throw new DIException(typeof(RegisterType), e);
            }
        }

        public RegisterType Resolve<RegisterType>(string name) where RegisterType : class
        {
            try
            {
                return _container.Resolve<RegisterType>(name);
            }
            catch (TinyIoC.TinyIoCResolutionException e)
            {
                throw new DIException(typeof(RegisterType), name, e);
            }
        }
    }

    [Serializable]
    public sealed class DIException : System.Exception
    {
        private const string TYPE_ERROR = "Unable to resolve type '{0}'";
        private const string NAMED_TYPE_ERROR = "Unable to resolve type '{0}' with name '{1}'";

        public DIException(Type type, Exception innerException)
            : base(string.Format(CultureInfo.InvariantCulture, TYPE_ERROR, type.FullName), innerException)
        {
        }

        public DIException(Type type, string name, Exception innerException)
            : base(string.Format(CultureInfo.InvariantCulture, NAMED_TYPE_ERROR, type.FullName, name), innerException)
        {
        }

        private DIException(System.Runtime.Serialization.SerializationInfo serializationInfo, System.Runtime.Serialization.StreamingContext streamingContext) : base(serializationInfo, streamingContext)
        {
        }
    }
}