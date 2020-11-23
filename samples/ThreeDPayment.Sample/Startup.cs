using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ThreeDPayment.Sample
{
    public class Startup
    {
        private readonly IConfiguration Configuration;
        private readonly IWebHostEnvironment HostEnvironment;

        public Startup(IConfiguration configuration,
            IWebHostEnvironment hostEnvironment)
        {
            Configuration = configuration;
            HostEnvironment = hostEnvironment;
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services"></param>
        public void ConfigureServices(IServiceCollection services)
        {
            //disable cookie policy for redirection from bank
            services.Configure<CookiePolicyOptions>(options => options.MinimumSameSitePolicy = SameSiteMode.None);

            //add response compression
            services.AddResponseCompression();

            //add session support
            services.AddSession();

            services.AddSingleton(typeof(ICreditCardResolver), typeof(CreditCardResolver));

            //add controller with views support
            var mvcBuilder = services.AddControllersWithViews();

            //enable razor runtime compilation on development
            if (HostEnvironment.IsDevelopment())
                mvcBuilder.AddRazorRuntimeCompilation();

            //register common payment services
            services.AddPaymentServices();
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            //enable forwarded headers for proxy server
            app.UseForwardedHeaders();

            //use https redirection
            app.UseHttpsRedirection();

            //use response compression
            app.UseResponseCompression();

            //use static file
            app.UseStaticFiles();

            //use session
            app.UseSession();

            //use routing
            app.UseRouting();

            //use authorization
            app.UseAuthorization();

            //use endpoint routing
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}
