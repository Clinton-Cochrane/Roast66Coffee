// Controllers/MenuController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;


namespace CoffeeShopApi.Controllers;
[ApiController]
[Route("api/[controller]")]
public class MenuController : ControllerBase
{
    private readonly MenuService _menuService;

    public MenuController(MenuService menuService)
    {
        _menuService = menuService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MenuItem>>> GetMenuItems()
    {
        return Ok(await _menuService.GetMenuItemsAsync());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<MenuItem>> PostMenuItem(MenuItem menuItem)
    {
        var createdItem = await _menuService.CreateMenuItemAsync(menuItem);
        return CreatedAtAction(nameof(GetMenuItems), new { id = createdItem.Id }, createdItem);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutMenuItem(int id, MenuItem menuItem)
    {
        if (id != menuItem.Id)
        {
            return BadRequest();
        }

        var result = await _menuService.UpdateMenuItemAsync(menuItem);
        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMenuItem(int id)
    {
        var result = await _menuService.DeleteMenuItemAsync(id);
        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }
}
