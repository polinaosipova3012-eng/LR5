using System;

namespace ConsoleApp1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Ввод переменных a, b и f
            Console.Write("Введите значение a: ");
            double a = Convert.ToDouble(Console.ReadLine());

            Console.Write("Введите значение b: ");
            double b = Convert.ToDouble(Console.ReadLine());

            Console.Write("Введите значение f: ");
            double f = Convert.ToDouble(Console.ReadLine());

            // Счетчик итераций
            int iterationCount = 0;

            // Проверка условия b - a > 2 * f
            while (b - a > 2 * f)
            {
                iterationCount++; // Увеличиваем счетчик итераций

                // Действие, если условие выполняется
                Console.WriteLine($"\nИтерация {iterationCount}:");
                Console.WriteLine("Условие выполнено: b - a > 2 * f");

                // Вычисление средней точки c
                double c = (a + b) / 2;
                Console.WriteLine($"Текущая средняя точка c: {c}");

                // Проверка условия sin(a) - 0.2a и sin(c) - 0.2c
                if (((Math.Sin(a) - 0.2 * a) * (Math.Sin(c) - (0.2 * c))) < 0)
                {
                    b = c; // Если условие выполняется, обновляем b
                    Console.WriteLine($"Обновлено значение b: {b}");
                }
                else
                {
                    a = c; // Если условие не выполняется, обновляем a
                    Console.WriteLine($"Обновлено значение a: {a}");
                }

                // Вывод текущих значений a, b и c
                Console.WriteLine($"Текущие значения: a = {a}, b = {b}, c = {c}");
            }

            // Если условие не выполняется, выводим x
            double x = (a + b) / 2; // Определяем x как среднее значение между a и b

            Console.WriteLine("\nУсловие не выполнено: b - a <= 2 * f");
            Console.WriteLine($"Значение x: {x}");

            // Ожидание ввода перед закрытием консоли
            Console.WriteLine("Нажмите Enter для выхода...");
            Console.ReadLine();
        }
    }
}
