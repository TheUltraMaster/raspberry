using System;
using System.Device.I2c;
using Iot.Device.Mpr121;
using System.Threading;
using Iot.Device.Pcx857x;
using Iot.Device.CharacterLcd;
using System.Device.Gpio;
using System.Device.Spi;
using Iot.Device.Mfrc522;
using Iot.Device.Rfid;
using Firebase.Database;
using Firebase.Database.Query;

class Program
{

    private static string firebaseURL = "https://proyectoraspberry-61ecf-default-rtdb.firebaseio.com/"; // Reemplaza con tu URL de Firebase Realtime Database

    static async Task Main(string[] args)
    {



        var firebaseClient = new FirebaseClient(firebaseURL);

        //configuraciones de RFID
        var spiConnectionSettings = new SpiConnectionSettings(0, 0)
        {
            ClockFrequency = 5000000, // 5 MHz
            Mode = SpiMode.Mode0
        };

        // Inicialización del 1dispositivo SPI y GpioController
        using SpiDevice spiDevice = SpiDevice.Create(spiConnectionSettings);
        using GpioController gpioController = new GpioController();
        using var mfrc522 = new MfRc522(spiDevice, pinReset: 25, gpioController, shouldDispose: true);



        //Ajuste de ganancia para mejorar la lectura
        Data106kbpsTypeA card; // Objeto para almacenar los datos de la tarjeta

        using I2cDevice i2c = I2cDevice.Create(new I2cConnectionSettings(1, 0x27));
        using var driver = new Pcf8574(i2c);
        using var lcd = new Lcd2004(registerSelectPin: 0,
                                enablePin: 2,
                                dataPins: new int[] { 4, 5, 6, 7 },
                                backlightPin: 3,
                                backlightBrightness: 0.1f,
                                readWritePin: 1,
                                controller: new GpioController(PinNumberingScheme.Logical, driver));








        string clave = "";
        // Configurar conexi�n I2C
        const int I2cBusId = 1; // Bus I2C 1 en Raspberry Pi
        const int Mpr121Address = 0x5A; // Direcci�n por defecto del MPR121

        var i2cSettings = new I2cConnectionSettings(I2cBusId, Mpr121Address);
        var i2cDevice = I2cDevice.Create(i2cSettings);

        // Inicializar el sensor MPR121
        using var mpr121 = new Mpr121(i2cDevice);


        Console.WriteLine("Iniciando lecturas del sensor MPR121...");




        while (true)
        {
            lcd.SetCursorPosition(0, 0);
            lcd.Write("Ingrese su clave:");
            bool cardDetected = mfrc522.ListenToCardIso14443TypeA(out card, TimeSpan.FromMilliseconds(100));
            if (cardDetected)
            {
                lcd.Clear();
                clave = "";
                lcd.SetCursorPosition(0, 0);
                lcd.Write("Detectando ...");

                Console.WriteLine($"UID: {BitConverter.ToString(card.NfcId)}"); // Usa el campo adecuado para el UID
                Usuario? usuario = await getUsuarioClave(firebaseClient, BitConverter.ToString(card.NfcId));


                lcd.Clear();

                lcd.Write(usuario != null ? usuario.nombre : "Error");

                Thread.Sleep(7000);
                lcd.Clear();
                lcd.SetCursorPosition(0, 0);
                lcd.Write("Ingrese su clave:");



            }

            var touchedPins = mpr121.ReadChannelStatuses();

            foreach (var pin in touchedPins)
            {

                if (pin.Value)
                {
                    int valor = int.Parse(pin.Key.ToString().Split('l').Last());

                    switch (valor)
                    {
                        case 11:
                            if (clave.Length == 8)
                            {


                                lcd.Clear();

                                lcd.SetCursorPosition(0, 0);
                                lcd.Write("Detectando ...");

                                Usuario? usuario = await getUsuarioContra(firebaseClient, clave);

                                clave = "";
                                lcd.Clear();

                                lcd.Write(usuario != null ? usuario.nombre : "Error");

                                Thread.Sleep(7000);
                                lcd.Clear();
                                lcd.SetCursorPosition(0, 0);
                                lcd.Write("Ingrese su clave:");



                            }
                            break;
                        case 10: if (0 < clave.Length) clave = clave.Remove(clave.Length - 1); break;
                        default: if (clave.Length < 8) clave = clave + valor; break;

                    }
                    int espacio = 8 - clave.Length;
                    string tmp = clave;
                    while (tmp.Length < 8)
                    {
                        tmp = tmp + " ";

                    }


                    lcd.SetCursorPosition(0, 1);
                    lcd.Write(tmp);

                    Console.WriteLine(clave);

                    break;


                }



            }




            if (Console.KeyAvailable)
            {

                lcd.Clear();
                break;
            }




        }


    }







    private async static Task<Usuario> getUsuarioClave(FirebaseClient firebaseClient, string clave)
    {
        try
        {
            var usuarios = await firebaseClient
                .Child("Usuarios")
                .OrderBy("clave")
                .EqualTo(clave)
                .OnceAsync<Usuario>();

            foreach (var usuario in usuarios)
            {
                if (usuario.Object.clave == clave)
                {
                    Usuario u = new Usuario();
                    u.nombre = usuario.Object.nombre;

                    return u;
                }
            }

            return null; // El usuario no existe
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al consultar usuario: {ex.Message}");
            return null;
        }
    }



    private async static Task<Usuario> getUsuarioContra(FirebaseClient firebaseClient, string clave)
    {
        try
        {
            var usuarios = await firebaseClient
                .Child("Usuarios")
                .OrderBy("contra")
                .EqualTo(clave)
                .OnceAsync<Usuario>();

            foreach (var usuario in usuarios)
            {
                if (usuario.Object.contra == clave)
                {
                    Usuario u = new Usuario();
                    u.nombre = usuario.Object.nombre;
                    return u;
                }
            }

            return null; // El usuario no existe
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al consultar usuario: {ex.Message}");
            return null;
        }
    }




    private static async Task AddAutenticacion(FirebaseClient firebaseClient, string id, string clave, string nombre)
    {
        try
        {
            // Obtener la fecha y hora actual
            string fechaActual = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            await firebaseClient
                .Child("Autenticaciones")
                .Child(id) // ID de la autenticación
                .PutAsync(new Autenticacion
                {
                    clave = clave,
                    fecha = fechaActual, // Guardar la fecha y hora actuales
                    nombre = nombre
                });

            Console.WriteLine($"Autenticación con ID {id} agregada correctamente en Firebase.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al agregar autenticación: {ex.Message}");
        }
    }














}
