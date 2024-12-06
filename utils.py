def validate_positive_float(value, param_name):
    """
    Validates if the given value is a positive float.
    
    Parameters:
        value (str): The input value as a string.
        param_name (str): The name of the parameter for error messages.
    
    Returns:
        float: The validated positive float value.
    
    Raises:
        ValueError: If the value is not a positive float.
    """
    try:
        val = float(value)
        if val <= 0:
            raise ValueError(f"{param_name} must be a positive number.")
        return val
    except ValueError:
        raise ValueError(f"{param_name} must be a valid positive number.")

def get_simulation_defaults():
    """
    Provides default parameter values for the simulation.
    
    Returns:
        dict: A dictionary containing default values for mass, initial temperature, fridge temperature, and HTC.
    """
    return {
        "mass": 0.5,              # kg
        "initial_temp": 90.0,     # °C
        "fridge_temp": 4.0,       # °C
        "htc": 10.0               # W/m²·K
    }

def log_simulation_inputs(mass, initial_temp, fridge_temp, htc):
    """
    Logs simulation parameters for debugging or output purposes.
    
    Parameters:
        mass (float): The mass of the object (kg).
        initial_temp (float): The initial temperature (°C).
        fridge_temp (float): The fridge temperature (°C).
        htc (float): The heat transfer coefficient (W/m²·K).
    """
    print("Simulation Parameters:")
    print(f"  Mass: {mass} kg")
    print(f"  Initial Temperature: {initial_temp} °C")
    print(f"  Fridge Temperature: {fridge_temp} °C")
    print(f"  Heat Transfer Coefficient: {htc} W/m²·K")

def generate_tooltips():
    """
    Provides a dictionary of tooltips for GUI components.
    
    Returns:
        dict: Tooltips for the GUI input fields.
    """
    return {
        "mass": "The mass of the object being cooled (kg).",
        "initial_temp": "The starting temperature of the object (°C).",
        "fridge_temp": "The temperature inside the fridge (°C).",
        "htc": "Heat transfer coefficient (W/m²·K), indicating how quickly heat is transferred."
    }

def apply_tooltips(widgets, tooltips):
    """
    Attaches tooltips to the provided widgets.

    Parameters:
        widgets (list): List of tkinter widget objects.
        tooltips (dict): Dictionary containing tooltips for each widget.
    """
    from tkinter import Toplevel, Label

    def show_tooltip(event, text):
        tooltip = Toplevel()
        tooltip.wm_overrideredirect(True)
        tooltip.geometry(f"+{event.x_root+10}+{event.y_root+10}")
        label = Label(tooltip, text=text, background="lightyellow", relief="solid", borderwidth=1)
        label.pack()
        event.widget.tooltip = tooltip

    def hide_tooltip(event):
        if hasattr(event.widget, "tooltip"):
            event.widget.tooltip.destroy()
            del event.widget.tooltip

    for widget, param in widgets:
        widget.bind("<Enter>", lambda e, t=tooltips[param]: show_tooltip(e, t))
        widget.bind("<Leave>", hide_tooltip)
