using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // MediatR — bulunduğu assembly'deki tüm Handler'ları otomatik kaydeder
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            // Once dogrulama: gecersiz istek handler'a (ve yeniden deneme dongusune) hic girmez
            cfg.AddOpenBehavior(typeof(Common.Behaviors.ValidationBehavior<,>));
            // Para/stok komutlari: eszamanli cakismada guncel veriyle yeniden dene
            cfg.AddOpenBehavior(typeof(Common.Behaviors.ConcurrencyRetryBehavior<,>));
        });

        // FluentValidation — aynı assembly'deki tüm Validator'ları otomatik kaydeder
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Her alanda ilk hatada dur: "Ad zorunludur. Ad en az 2 karakter..." gibi
        // aynı alan için art arda mesaj yerine kullanıcı tek, net bir mesaj görür.
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;

        return services;
    }
}