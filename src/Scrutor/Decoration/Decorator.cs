﻿using Microsoft.Extensions.DependencyInjection;
using System;

namespace Scrutor.Decoration;

internal readonly struct Decorator
{
    private Decorator(IDecoratorStrategy decoratorStrategy)
        => DecoratorStrategy = decoratorStrategy;

    private IDecoratorStrategy DecoratorStrategy { get; }

    public static Decorator Create(Type serviceType, Type? decoratorType, Func<object, IServiceProvider, object>? decoratorFactory)
    {
        IDecoratorStrategy strategy;

        if (serviceType.IsOpenGeneric())
        {
            strategy = new OpenGenericDecoratorStrategy(serviceType, decoratorType, decoratorFactory);
        }
        else
        {
            strategy = new ClosedTypeDecoratorStrategy(serviceType, decoratorType, decoratorFactory);
        }

        return new Decorator(strategy);
    }

    public IServiceCollection Decorate(IServiceCollection services)
    {
        if (TryDecorate(services))
        {
            return services;
        }

        throw new MissingTypeRegistrationException(DecoratorStrategy.ServiceType);

    }

    public bool TryDecorate(IServiceCollection services)
    {
        var decorated = false;

        for (var i = services.Count - 1; i >= 0; i--)
        {
            var serviceDescriptor = services[i];

            if (serviceDescriptor.ServiceType is DecoratedType)
            {
                continue; // Service has already been decorated.
            }

            if (!DecoratorStrategy.CanDecorate(serviceDescriptor.ServiceType))
            {
                continue; // Unable to decorate using the specified strategy.
            }

            var decoratedType = new DecoratedType(serviceDescriptor.ServiceType);

            // insert decorated
            services.Add(serviceDescriptor.WithServiceType(decoratedType));

            // replace decorator
            var decoratorFactory = DecoratorStrategy.CreateDecorator(decoratedType);
            services[i] = new ServiceDescriptor(serviceDescriptor.ServiceType, decoratorFactory, serviceDescriptor.Lifetime);

            decorated = true;
        }

        return decorated;
    }
}
