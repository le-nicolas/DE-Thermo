import matplotlib.pyplot as plt
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg

class DynamicPlot:
    def __init__(self, parent):
        self.fig, self.ax = plt.subplots(figsize=(5, 3))
        self.ax.set_title("Temperature vs. Time")
        self.ax.set_xlabel("Time (s)")
        self.ax.set_ylabel("Temperature (°C)")

        self.canvas = FigureCanvasTkAgg(self.fig, master=parent)
        self.canvas.get_tk_widget().pack()

    def plot_results(self, time, temperature):
        self.ax.plot(time, temperature, label="Cooling Curve")
        self.ax.axhline(0, color="blue", linestyle="--", label="Freezing Point")
        self.ax.legend()
        self.canvas.draw()

    def clear_plot(self):
        self.ax.clear()
        self.ax.set_title("Temperature vs. Time")
        self.ax.set_xlabel("Time (s)")
        self.ax.set_ylabel("Temperature (°C)")
