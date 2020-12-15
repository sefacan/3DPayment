using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ThreeDPayment.Sample.Data;
using ThreeDPayment.Sample.Services;

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

            //add sqlite db context
            services.AddDbContext<AppDataContext>(options => {
                options.UseSqlite("Data Source=ThreeDPayment.db");
            });

            //register db services
            services.AddScoped<IBankService, BankService>();
            services.AddScoped<IPaymentService, PaymentService>();

            //register common payment services
            services.AddPaymentServices();

            //add controller with views support
            var mvcBuilder = services.AddControllersWithViews();

            //add newtonsoft json serializer
            mvcBuilder.AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Local;
                options.SerializerSettings.Formatting = Formatting.Indented;
                options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            });

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                DateTimeZoneHandling = DateTimeZoneHandling.Local,
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            //enable razor runtime compilation on development
            if (HostEnvironment.IsDevelopment())
                mvcBuilder.AddRazorRuntimeCompilation();
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

            //initialize database
            app.InitializeDatabase();

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
                //confirm
                endpoints.MapControllerRoute(
                    name: "Confirm",
                    pattern: "payment/confirm/{paymentId:Guid?}",
                    defaults: new { action = "Confirm", controller = "Payment" });

                //callback
                endpoints.MapControllerRoute(
                    name: "Callback",
                    pattern: "payment/callback/{paymentId:Guid?}",
                    defaults: new { action = "Callback", controller = "Payment" });

                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}
